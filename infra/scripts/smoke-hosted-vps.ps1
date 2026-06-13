param(
    [string]$EnvFile = "",
    [string]$WebBaseUrl = "",
    [string]$ApiBaseUrl = "",
    [int]$TimeoutSeconds = 15,
    [switch]$SkipAuthPages,
    [switch]$SkipPwaChecks,
    [switch]$ResolveOnly
)

$ErrorActionPreference = "Stop"

function Read-EnvFile {
    param([string]$Path)

    $values = @{}
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $values
    }

    $resolvedPath = Resolve-Path $Path
    foreach ($line in Get-Content -LiteralPath $resolvedPath.Path) {
        if ($line -notmatch '^\s*([^#=\s]+)\s*=(.*)$') {
            continue
        }

        $key = $Matches[1].Trim()
        $value = $Matches[2]
        $commentIndex = $value.IndexOf("#")
        if ($commentIndex -ge 0) {
            $value = $value.Substring(0, $commentIndex)
        }

        $values[$key] = $value.Trim().Trim('"').Trim("'")
    }

    return $values
}

function Get-ConfigValue {
    param(
        [hashtable]$Values,
        [string[]]$Keys,
        [string]$Fallback
    )

    foreach ($key in $Keys) {
        if ($Values.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$key])) {
            return [string]$Values[$key]
        }

        $processValue = [Environment]::GetEnvironmentVariable($key)
        if (-not [string]::IsNullOrWhiteSpace($processValue)) {
            return $processValue
        }
    }

    return $Fallback
}

function Join-Url {
    param(
        [string]$BaseUrl,
        [string]$Path
    )

    $base = $BaseUrl.TrimEnd("/") + "/"
    $relative = $Path.TrimStart("/")
    return ([Uri]::new([Uri]$base, $relative)).AbsoluteUri
}

function Invoke-SmokeRequest {
    param(
        [string]$Url,
        [string]$Context,
        [string]$ExpectedContentType = ""
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec $TimeoutSeconds -UseBasicParsing
    }
    catch {
        throw "$Context failed at $Url. $($_.Exception.Message)"
    }

    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "$Context expected HTTP 2xx at $Url, got HTTP $($response.StatusCode)."
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedContentType)) {
        $contentType = [string]$response.Headers["Content-Type"]
        if ([string]::IsNullOrWhiteSpace($contentType)) {
            $contentType = [string]$response.ContentType
        }

        if ([string]::IsNullOrWhiteSpace($contentType) -or $contentType.IndexOf($ExpectedContentType, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "$Context expected content type containing '$ExpectedContentType', got '$contentType'."
        }
    }

    return $response
}

function Get-SmokeResponseText {
    param([object]$Response)

    if ($Response.Content -is [byte[]]) {
        return [System.Text.Encoding]::UTF8.GetString([byte[]]$Response.Content)
    }

    return [string]$Response.Content
}

function Assert-JsonStatus {
    param(
        [string]$Url,
        [string]$ExpectedStatus,
        [string]$Context
    )

    $response = Invoke-SmokeRequest -Url $Url -Context $Context -ExpectedContentType "json"
    $body = Get-SmokeResponseText $response | ConvertFrom-Json
    if ($body.status -ne $ExpectedStatus) {
        throw "$Context expected status '$ExpectedStatus', got '$($body.status)'."
    }
}

function Assert-Page {
    param(
        [string]$Url,
        [string]$Context
    )

    $response = Invoke-SmokeRequest -Url $Url -Context $Context -ExpectedContentType "html"
    if ([string]::IsNullOrWhiteSpace([string]$response.Content)) {
        throw "$Context returned an empty HTML body at $Url."
    }
}

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$envFileValues = Read-EnvFile $EnvFile

if ([string]::IsNullOrWhiteSpace($WebBaseUrl)) {
    $WebBaseUrl = Get-ConfigValue $envFileValues @("APP_FRONTEND_BASE_URL", "E2E_BASE_URL") "http://localhost:3000"
}

if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    $ApiBaseUrl = Get-ConfigValue $envFileValues @("APP_API_BASE_URL", "NEXT_PUBLIC_API_URL") "http://localhost:5000"
}

$WebBaseUrl = $WebBaseUrl.TrimEnd("/")
$ApiBaseUrl = $ApiBaseUrl.TrimEnd("/")

if ($ResolveOnly) {
    Write-Host "Resolved hosted VPS smoke configuration:" -ForegroundColor Cyan
    Write-Host "  WebBaseUrl: $WebBaseUrl"
    Write-Host "  ApiBaseUrl: $ApiBaseUrl"
    Write-Host "  SkipAuthPages: $SkipAuthPages"
    Write-Host "  SkipPwaChecks: $SkipPwaChecks"
    exit 0
}

Write-Step "API readiness"
Assert-JsonStatus -Url (Join-Url $ApiBaseUrl "/ready") -ExpectedStatus "ready" -Context "API readiness"

Write-Step "API health"
Assert-JsonStatus -Url (Join-Url $ApiBaseUrl "/health") -ExpectedStatus "healthy" -Context "API health"

Write-Step "Frontend landing page"
Assert-Page -Url (Join-Url $WebBaseUrl "/") -Context "Frontend landing page"

if (-not $SkipAuthPages) {
    Write-Step "Public auth pages"
    foreach ($path in @("/login", "/register", "/forgot-password", "/reset-password")) {
        Assert-Page -Url (Join-Url $WebBaseUrl $path) -Context "Public auth page $path"
    }
}

if (-not $SkipPwaChecks) {
    Write-Step "PWA manifest"
    $manifestResponse = Invoke-SmokeRequest -Url (Join-Url $WebBaseUrl "/manifest.json") -Context "PWA manifest" -ExpectedContentType "json"
    $manifest = Get-SmokeResponseText $manifestResponse | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$manifest.name) -or [string]::IsNullOrWhiteSpace([string]$manifest.short_name)) {
        throw "PWA manifest is missing name or short_name."
    }
    if ($manifest.display -ne "standalone") {
        throw "PWA manifest expected display 'standalone', got '$($manifest.display)'."
    }
    if ([string]::IsNullOrWhiteSpace([string]$manifest.start_url)) {
        throw "PWA manifest is missing start_url."
    }
    if ($null -eq $manifest.icons -or $manifest.icons.Count -lt 1) {
        throw "PWA manifest must include at least one icon."
    }

    $firstIcon = [string]$manifest.icons[0].src
    if (-not [string]::IsNullOrWhiteSpace($firstIcon)) {
        Invoke-SmokeRequest -Url (Join-Url $WebBaseUrl $firstIcon) -Context "PWA icon $firstIcon" | Out-Null
    }

    Write-Step "Service worker"
    $serviceWorker = Invoke-SmokeRequest -Url (Join-Url $WebBaseUrl "/sw.js") -Context "Service worker" -ExpectedContentType "javascript"
    $serviceWorkerText = Get-SmokeResponseText $serviceWorker
    if (-not $serviceWorkerText.Contains("addEventListener")) {
        throw "Service worker response did not look like a service worker script."
    }
}

Write-Host "Hosted VPS smoke checks passed." -ForegroundColor Green
