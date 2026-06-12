param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $ShifterDir).Path
$script = Join-Path $root "infra\scripts\setup-github-staging.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shifter-staging-setup-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $fakeGh = Join-Path $tempDir "gh.ps1"
    $callLog = Join-Path $tempDir "gh-calls.log"
    @'
Add-Content -LiteralPath $env:SHIFTER_FAKE_GH_CALL_LOG -Value ($args -join " ")
exit 0
'@ | Set-Content -LiteralPath $fakeGh -Encoding ASCII

    $powerShellExe = (Get-Process -Id $PID).Path
    $baseCommand = @("-NoProfile")
    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        $baseCommand += @("-ExecutionPolicy", "Bypass")
    }

    $env:SHIFTER_FAKE_GH_CALL_LOG = $callLog

    $dryOutput = & $powerShellExe @baseCommand -File $script `
        -GhPath $fakeGh `
        -WebBaseUrl "https://staging.example.com" `
        -ApiBaseUrl "https://staging-api.example.com" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected dry-run setup to pass. Output:`n$($dryOutput | Out-String)"
    }
    if (Test-Path -LiteralPath $callLog) {
        throw "Dry-run setup should not call gh."
    }
    $dryText = $dryOutput | Out-String
    if ($dryText -notmatch [regex]::Escape("Dry run only. Re-run with -Apply")) {
        throw "Dry-run output did not explain apply mode. Output:`n$dryText"
    }

    $applyOutput = & $powerShellExe @baseCommand -File $script `
        -GhPath $fakeGh `
        -WebBaseUrl "https://staging.example.com" `
        -ApiBaseUrl "https://staging-api.example.com" `
        -StagingPath "/srv/shifter-staging" `
        -ComposeProjectName "shifter-stage" `
        -EnablePushDeploy `
        -Apply 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected apply setup to pass. Output:`n$($applyOutput | Out-String)"
    }

    $calls = Get-Content -LiteralPath $callLog -Raw
    foreach ($pattern in @(
            "api -X PUT repos/OfekItzhaki/Shifter/environments/staging",
            "variable set ENABLE_STAGING_DEPLOY --repo OfekItzhaki/Shifter --body true",
            "variable set STAGING_PATH --repo OfekItzhaki/Shifter --body /srv/shifter-staging",
            "variable set STAGING_COMPOSE_PROJECT_NAME --repo OfekItzhaki/Shifter --body shifter-stage",
            "variable set STAGING_WEB_BASE_URL --repo OfekItzhaki/Shifter --body https://staging.example.com",
            "variable set STAGING_API_BASE_URL --repo OfekItzhaki/Shifter --body https://staging-api.example.com"
        )) {
        if ($calls -notmatch [regex]::Escape($pattern)) {
            throw "Apply mode did not call '$pattern'. Calls:`n$calls"
        }
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $missingOutput = & $powerShellExe @baseCommand -File $script `
            -GhPath $fakeGh `
            -WebBaseUrl "https://staging.example.com" `
            -Apply 2>&1
        $missingExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($missingExitCode -eq 0) {
        throw "Expected missing ApiBaseUrl setup to fail. Output:`n$($missingOutput | Out-String)"
    }
    if (($missingOutput | Out-String) -notmatch [regex]::Escape("ApiBaseUrl is required.")) {
        throw "Missing ApiBaseUrl failure was not clear. Output:`n$($missingOutput | Out-String)"
    }
}
finally {
    Remove-Item Env:\SHIFTER_FAKE_GH_CALL_LOG -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "GitHub staging setup test passed." -ForegroundColor Green
