param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Start-MockHttpServer {
    param(
        [ValidateSet("api", "web")]
        [string]$Kind,
        [string]$StopFile,
        [string]$RequiredAuthorization = ""
    )

    $port = Get-FreeTcpPort
    $job = Start-Job -ScriptBlock {
        param(
            [int]$Port,
            [string]$Kind,
            [string]$StopFile,
            [string]$RequiredAuthorization
        )

        $ErrorActionPreference = "Stop"
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        try {
            while (-not (Test-Path -LiteralPath $StopFile)) {
                if (-not $listener.Pending()) {
                    Start-Sleep -Milliseconds 25
                    continue
                }

                $client = $listener.AcceptTcpClient()
                try {
                    $stream = $client.GetStream()
                    $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 1024, $true)
                    $requestLine = $reader.ReadLine()
                    $authorization = ""
                    while ($true) {
                        $headerLine = $reader.ReadLine()
                        if ($null -eq $headerLine -or [string]::IsNullOrWhiteSpace($headerLine)) {
                            break
                        }

                        if ($headerLine -match '^Authorization:\s*(.+)$') {
                            $authorization = $Matches[1].Trim()
                        }
                    }

                    $path = "/"
                    if ($requestLine -match '^\S+\s+(\S+)\s+HTTP/') {
                        $target = $Matches[1]
                        if ($target -match '^https?://') {
                            $path = ([Uri]$target).AbsolutePath
                        }
                        else {
                            $path = ([Uri]::new("http://localhost$target")).AbsolutePath
                        }
                    }

                    $status = "200 OK"
                    $contentType = "text/plain"
                    $body = "ok"

                    if (-not [string]::IsNullOrWhiteSpace($RequiredAuthorization) -and $authorization -ne $RequiredAuthorization) {
                        $status = "401 Unauthorized"
                        $body = "unauthorized"
                    }
                    elseif ($Kind -eq "api") {

                        if ($path -eq "/ready") {
                            $contentType = "application/json"
                            $body = '{"status":"ready"}'
                        }
                        elseif ($path -eq "/health") {
                            $contentType = "application/json"
                            $body = '{"status":"healthy"}'
                        }
                        else {
                            $status = "404 Not Found"
                            $body = "not found"
                        }
                    }
                    else {
                        if ($path -in @("/", "/login", "/register", "/forgot-password", "/reset-password")) {
                            $contentType = "text/html; charset=utf-8"
                            $body = "<!doctype html><html><body>Shifter $path</body></html>"
                        }
                        elseif ($path -eq "/manifest.json") {
                            $contentType = "application/manifest+json"
                            $body = '{"name":"Shifter","short_name":"Shifter","display":"standalone","start_url":"/","icons":[{"src":"/icon.png","sizes":"192x192","type":"image/png"}]}'
                        }
                        elseif ($path -eq "/icon.png") {
                            $contentType = "image/png"
                            $body = "mock-icon"
                        }
                        elseif ($path -eq "/sw.js") {
                            $contentType = "application/javascript"
                            $body = "self.addEventListener('install', function () {});"
                        }
                        else {
                            $status = "404 Not Found"
                            $body = "not found"
                        }
                    }

                    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)
                    $header = "HTTP/1.1 $status`r`nContent-Type: $contentType`r`nContent-Length: $($bodyBytes.Length)`r`nConnection: close`r`n`r`n"
                    $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($header)
                    $stream.Write($headerBytes, 0, $headerBytes.Length)
                    $stream.Write($bodyBytes, 0, $bodyBytes.Length)
                    $stream.Flush()
                }
                finally {
                    $client.Close()
                }
            }
        }
        finally {
            $listener.Stop()
        }
    } -ArgumentList $port, $Kind, $StopFile, $RequiredAuthorization

    return [pscustomobject]@{
        Port = $port
        Job = $job
        BaseUrl = "http://127.0.0.1:$port"
    }
}

function Stop-MockHttpServer {
    param(
        [object]$Server,
        [string]$StopFile
    )

    if ($null -ne $Server) {
        New-Item -ItemType File -Path $StopFile -Force | Out-Null
        Wait-Job -Job $Server.Job -Timeout 5 | Out-Null
        Stop-Job -Job $Server.Job -ErrorAction SilentlyContinue
        Remove-Job -Job $Server.Job -Force -ErrorAction SilentlyContinue
    }
}

$root = (Resolve-Path $ShifterDir).Path
$script = Join-Path $root "infra\scripts\smoke-hosted-vps.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shifter-hosted-smoke-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

$apiStopFile = Join-Path $tempDir "api.stop"
$webStopFile = Join-Path $tempDir "web.stop"
$apiServer = $null
$webServer = $null

try {
    $apiServer = Start-MockHttpServer -Kind api -StopFile $apiStopFile
    $webServer = Start-MockHttpServer -Kind web -StopFile $webStopFile
    Start-Sleep -Milliseconds 250

    $envFile = Join-Path $tempDir ".env"
    @(
        "APP_FRONTEND_BASE_URL=$($webServer.BaseUrl)",
        "APP_API_BASE_URL=$($apiServer.BaseUrl)"
    ) | Set-Content -LiteralPath $envFile -Encoding ASCII

    $powerShellExe = (Get-Process -Id $PID).Path
    $isWindowsHost = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
    $command = @("-NoProfile")
    if ($isWindowsHost) {
        $command += @("-ExecutionPolicy", "Bypass")
    }
    $command += @("-File", $script, "-EnvFile", $envFile, "-TimeoutSeconds", "5")

    $output = & $powerShellExe @command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Hosted VPS smoke script failed with exit code $LASTEXITCODE. Output:`n$($output | Out-String)"
    }

    $text = $output | Out-String
    foreach ($pattern in @(
            "API readiness",
            "API health",
            "Frontend landing page",
            "Public auth pages",
            "PWA manifest",
            "Service worker",
            "Hosted VPS smoke checks passed."
        )) {
        if ($text -notmatch [regex]::Escape($pattern)) {
            throw "Hosted VPS smoke output is missing '$pattern'. Output:`n$text"
        }
    }

    Stop-MockHttpServer -Server $apiServer -StopFile $apiStopFile
    Stop-MockHttpServer -Server $webServer -StopFile $webStopFile
    $apiServer = $null
    $webServer = $null

    $username = "staging-user"
    $password = "staging-pass"
    $requiredAuthorization = "Basic " + [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("${username}:${password}"))
    $apiStopFile = Join-Path $tempDir "api-auth.stop"
    $webStopFile = Join-Path $tempDir "web-auth.stop"
    $apiServer = Start-MockHttpServer -Kind api -StopFile $apiStopFile -RequiredAuthorization $requiredAuthorization
    $webServer = Start-MockHttpServer -Kind web -StopFile $webStopFile -RequiredAuthorization $requiredAuthorization
    Start-Sleep -Milliseconds 250

    $unprotectedEnvFile = Join-Path $tempDir ".protected-without-auth.env"
    @(
        "APP_FRONTEND_BASE_URL=$($webServer.BaseUrl)",
        "APP_API_BASE_URL=$($apiServer.BaseUrl)"
    ) | Set-Content -LiteralPath $unprotectedEnvFile -Encoding ASCII

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $unauthorizedCommand = @("-NoProfile")
        if ($isWindowsHost) {
            $unauthorizedCommand += @("-ExecutionPolicy", "Bypass")
        }
        $unauthorizedCommand += @("-File", $script, "-EnvFile", $unprotectedEnvFile, "-TimeoutSeconds", "5")
        $unauthorizedOutput = & $powerShellExe @unauthorizedCommand 2>&1
        $unauthorizedExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($unauthorizedExitCode -eq 0) {
        throw "Expected protected hosted VPS smoke without Basic Auth to fail. Output:`n$($unauthorizedOutput | Out-String)"
    }
    if (($unauthorizedOutput | Out-String) -notmatch "401|Unauthorized") {
        throw "Protected hosted VPS smoke without Basic Auth did not report an authorization failure. Output:`n$($unauthorizedOutput | Out-String)"
    }

    $protectedEnvFile = Join-Path $tempDir ".protected.env"
    @(
        "APP_FRONTEND_BASE_URL=$($webServer.BaseUrl)",
        "APP_API_BASE_URL=$($apiServer.BaseUrl)",
        "STAGING_BASIC_AUTH_USERNAME=$username",
        "STAGING_BASIC_AUTH_PASSWORD=$password"
    ) | Set-Content -LiteralPath $protectedEnvFile -Encoding ASCII

    $protectedCommand = @("-NoProfile")
    if ($isWindowsHost) {
        $protectedCommand += @("-ExecutionPolicy", "Bypass")
    }
    $protectedCommand += @("-File", $script, "-EnvFile", $protectedEnvFile, "-TimeoutSeconds", "5")
    $protectedOutput = & $powerShellExe @protectedCommand 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Hosted VPS smoke script with Basic Auth failed with exit code $LASTEXITCODE. Output:`n$($protectedOutput | Out-String)"
    }

    if (($protectedOutput | Out-String) -notmatch [regex]::Escape("Hosted VPS smoke checks passed.")) {
        throw "Protected hosted VPS smoke output did not show success. Output:`n$($protectedOutput | Out-String)"
    }
}
finally {
    Stop-MockHttpServer -Server $apiServer -StopFile $apiStopFile
    Stop-MockHttpServer -Server $webServer -StopFile $webStopFile
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Hosted VPS smoke script test passed." -ForegroundColor Green
