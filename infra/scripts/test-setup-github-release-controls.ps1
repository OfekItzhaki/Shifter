param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $ShifterDir).Path
$script = Join-Path $root "infra\scripts\setup-github-release-controls.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shifter-release-controls-setup-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $fakeGh = Join-Path $tempDir "gh.ps1"
    $callLog = Join-Path $tempDir "gh-calls.log"
    @'
$joined = $args -join " "

if ($joined -eq "api repos/OfekItzhaki/Shifter/rulesets") {
    '[{"id":17292274,"name":"Main"}]'
    exit 0
}

Add-Content -LiteralPath $env:SHIFTER_FAKE_GH_CALL_LOG -Value $joined

$inputIndex = [Array]::IndexOf($args, "--input")
if ($inputIndex -ge 0 -and $inputIndex + 1 -lt $args.Count) {
    $inputPath = $args[$inputIndex + 1]
    $payload = Get-Content -LiteralPath $inputPath -Raw
    Add-Content -LiteralPath $env:SHIFTER_FAKE_GH_CALL_LOG -Value $payload
}

exit 0
'@ | Set-Content -LiteralPath $fakeGh -Encoding ASCII

    $powerShellExe = (Get-Process -Id $PID).Path
    $baseCommand = @("-NoProfile")
    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        $baseCommand += @("-ExecutionPolicy", "Bypass")
    }

    $env:SHIFTER_FAKE_GH_CALL_LOG = $callLog

    $dryOutput = & $powerShellExe @baseCommand -File $script -GhPath $fakeGh 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected dry-run setup to pass. Output:`n$($dryOutput | Out-String)"
    }
    if (Test-Path -LiteralPath $callLog) {
        throw "Dry-run setup should not write release controls."
    }
    if (($dryOutput | Out-String) -notmatch [regex]::Escape("Dry run only. Re-run with -Apply")) {
        throw "Dry-run output did not explain apply mode. Output:`n$($dryOutput | Out-String)"
    }

    $applyOutput = & $powerShellExe @baseCommand -File $script `
        -GhPath $fakeGh `
        -Apply 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected apply setup to pass. Output:`n$($applyOutput | Out-String)"
    }

    $calls = Get-Content -LiteralPath $callLog -Raw
    foreach ($pattern in @(
            "api -X PUT repos/OfekItzhaki/Shifter/rulesets/17292274 --input",
            "api -X POST repos/OfekItzhaki/Shifter/rulesets --input"
        )) {
        if ($calls -notmatch [regex]::Escape($pattern)) {
            throw "Apply mode did not include '$pattern'. Calls:`n$calls"
        }
    }

    foreach ($pattern in @(
            '"type"\s*:\s*"pull_request"',
            '"type"\s*:\s*"required_status_checks"',
            '"context"\s*:\s*"API Build (\\u0026|&) Test"',
            '"context"\s*:\s*"Frontend Build"',
            '"context"\s*:\s*"Solver Lint (\\u0026|&) Test"',
            '"context"\s*:\s*"Package Preflight"',
            '"include"\s*:\s*\[',
            '"refs/heads/develop"'
        )) {
        if ($calls -notmatch $pattern) {
            throw "Apply mode did not match '$pattern'. Calls:`n$calls"
        }
    }

    $missingFailed = $false
    try {
        & $script -GhPath $fakeGh -RequiredStatusChecks @("") -Apply 2>&1 | Out-String | Out-Null
    }
    catch {
        $missingFailed = $true
        $missingOutput = $_.Exception.Message
    }

    if (-not $missingFailed) {
        throw "Expected missing RequiredStatusChecks setup to fail."
    }
    if ($missingOutput -notmatch [regex]::Escape("RequiredStatusChecks must include at least one check name.")) {
        throw "Missing RequiredStatusChecks failure was not clear. Output:`n$missingOutput"
    }
}
finally {
    Remove-Item Env:\SHIFTER_FAKE_GH_CALL_LOG -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "GitHub release controls setup test passed." -ForegroundColor Green
