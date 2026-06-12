param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $ShifterDir).Path
$script = Join-Path $root "infra\scripts\check-github-release-controls.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shifter-release-controls-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $fakeGh = Join-Path $tempDir "gh.ps1"
    @'
$mode = $env:SHIFTER_FAKE_RELEASE_CONTROLS_MODE
$joined = $args -join " "

if ($joined -like "api repos/*/rulesets") {
    '[{"id":1},{"id":2}]'
    exit 0
}

if ($joined -like "api repos/*/rulesets/1") {
    if ($mode -eq "ready") {
        '{"id":1,"name":"Main","enforcement":"active","conditions":{"ref_name":{"include":["~DEFAULT_BRANCH"],"exclude":[]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"},{"type":"pull_request"},{"type":"required_status_checks"}]}'
    }
    else {
        '{"id":1,"name":"Main","enforcement":"active","conditions":{"ref_name":{"include":["~DEFAULT_BRANCH"],"exclude":[]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"}]}'
    }
    exit 0
}

if ($joined -like "api repos/*/rulesets/2") {
    if ($mode -eq "ready") {
        '{"id":2,"name":"Develop","enforcement":"active","conditions":{"ref_name":{"include":["refs/heads/develop"],"exclude":[]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"}]}'
    }
    else {
        '{"id":2,"name":"Develop","enforcement":"evaluate","conditions":{"ref_name":{"include":["refs/heads/develop"],"exclude":[]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"}]}'
    }
    exit 0
}

Write-Error "Unexpected gh args: $joined"
exit 1
'@ | Set-Content -LiteralPath $fakeGh -Encoding ASCII

    $powerShellExe = (Get-Process -Id $PID).Path
    $baseCommand = @("-NoProfile")
    if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        $baseCommand += @("-ExecutionPolicy", "Bypass")
    }

    $env:SHIFTER_FAKE_RELEASE_CONTROLS_MODE = "ready"
    $readyOutput = & $powerShellExe @baseCommand -File $script -GhPath $fakeGh 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected ready release controls to pass. Output:`n$($readyOutput | Out-String)"
    }
    $readyText = $readyOutput | Out-String
    foreach ($pattern in @(
            "[PASS] main requires pull requests.",
            "[PASS] main requires status checks.",
            "[PASS] develop blocks deletion and force pushes.",
            "Summary: 0 failed"
        )) {
        if ($readyText -notmatch [regex]::Escape($pattern)) {
            throw "Ready output missing '$pattern'. Output:`n$readyText"
        }
    }

    $env:SHIFTER_FAKE_RELEASE_CONTROLS_MODE = "missing"
    $missingOutput = & $powerShellExe @baseCommand -File $script -GhPath $fakeGh 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw "Expected missing release controls to fail. Output:`n$($missingOutput | Out-String)"
    }
    $missingText = $missingOutput | Out-String
    foreach ($pattern in @(
            "[FAIL] main must require pull requests before production merges.",
            "[FAIL] main must require status checks before production merges.",
            "[WARN] develop does not have active no-delete/no-force-push rules.",
            "Summary:"
        )) {
        if ($missingText -notmatch [regex]::Escape($pattern)) {
            throw "Missing output missing '$pattern'. Output:`n$missingText"
        }
    }
}
finally {
    Remove-Item Env:\SHIFTER_FAKE_RELEASE_CONTROLS_MODE -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "GitHub release controls test passed." -ForegroundColor Green
