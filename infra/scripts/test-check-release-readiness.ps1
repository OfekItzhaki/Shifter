param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $ShifterDir).Path
$script = Join-Path $root "infra\scripts\check-release-readiness.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shifter-release-readiness-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $fakeGh = Join-Path $tempDir "gh.ps1"
    @'
$mode = $env:SHIFTER_FAKE_GH_MODE
$joined = $args -join " "

if ($joined -like "variable list*") {
    if ($mode -eq "ready") {
        '[{"name":"ENABLE_STAGING_DEPLOY"},{"name":"STAGING_WEB_BASE_URL"},{"name":"STAGING_API_BASE_URL"},{"name":"STAGING_PATH"},{"name":"STAGING_COMPOSE_PROJECT_NAME"}]'
    }
    else {
        '[]'
    }
    exit 0
}

if ($joined -like "secret list*") {
    if ($mode -eq "ready") {
        '[{"name":"STAGING_HOST"},{"name":"STAGING_USER"},{"name":"STAGING_SSH_KEY"}]'
    }
    else {
        '[{"name":"VPS_HOST"},{"name":"VPS_USER"},{"name":"VPS_SSH_KEY"}]'
    }
    exit 0
}

if ($joined -like "api repos/*/environments*") {
    if ($mode -eq "ready") {
        '{"environments":[{"name":"staging"}]}'
    }
    else {
        '{"environments":[]}'
    }
    exit 0
}

if ($joined -like "api repos/*/rulesets/1*") {
    if ($mode -eq "ready") {
        '{"id":1,"name":"Main","enforcement":"active","conditions":{"ref_name":{"include":["~DEFAULT_BRANCH"]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"},{"type":"pull_request"},{"type":"required_status_checks","parameters":{"required_status_checks":[{"context":"API Build & Test"},{"context":"Frontend Build"},{"context":"Solver Lint & Test"},{"context":"Package Preflight"}],"strict_required_status_checks_policy":true}}]}'
    }
    else {
        '{"id":1,"name":"Main","enforcement":"active","conditions":{"ref_name":{"include":["~DEFAULT_BRANCH"]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"}]}'
    }
    exit 0
}

if ($joined -like "api repos/*/rulesets/2*") {
    if ($mode -eq "ready") {
        '{"id":2,"name":"Develop","enforcement":"active","conditions":{"ref_name":{"include":["refs/heads/develop"]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"}]}'
    }
    else {
        '{"id":2,"name":"Develop","enforcement":"disabled","conditions":{"ref_name":{"include":["refs/heads/develop"]}},"rules":[{"type":"deletion"},{"type":"non_fast_forward"}]}'
    }
    exit 0
}

if ($joined -like "api repos/*/rulesets*") {
    '[{"id":1},{"id":2}]'
    exit 0
}

if ($joined -like "run list*") {
    if ($mode -eq "ready") {
        '[{"databaseId":101,"workflowName":"CI","status":"completed","conclusion":"success","headSha":"abcdef1234567890","createdAt":"2026-06-12T00:00:00Z","url":"https://example.invalid/ci","event":"workflow_dispatch"},{"databaseId":102,"workflowName":"Customer-Hosted Preflight","status":"completed","conclusion":"success","headSha":"abcdef1234567890","createdAt":"2026-06-12T00:00:00Z","url":"https://example.invalid/preflight","event":"push"},{"databaseId":103,"workflowName":"Deploy Staging","status":"completed","conclusion":"success","headSha":"abcdef1234567890","createdAt":"2026-06-12T00:00:00Z","url":"https://example.invalid/staging","event":"workflow_dispatch"}]'
    }
    else {
        '[{"databaseId":101,"workflowName":"CI","status":"completed","conclusion":"success","headSha":"abcdef1234567890","createdAt":"2026-06-12T00:00:00Z","url":"https://example.invalid/ci","event":"workflow_dispatch"},{"databaseId":102,"workflowName":"Customer-Hosted Preflight","status":"completed","conclusion":"success","headSha":"abcdef1234567890","createdAt":"2026-06-12T00:00:00Z","url":"https://example.invalid/preflight","event":"push"},{"databaseId":103,"workflowName":"Deploy Staging","status":"completed","conclusion":"skipped","headSha":"abcdef1234567890","createdAt":"2026-06-12T00:00:00Z","url":"https://example.invalid/staging","event":"push"}]'
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

    $env:SHIFTER_FAKE_GH_MODE = "ready"
    $readyOutput = & $powerShellExe @baseCommand -File $script -GhPath $fakeGh -SkipGitCheck -SkipHostedSmoke 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected ready audit to pass. Output:`n$($readyOutput | Out-String)"
    }
    $readyText = $readyOutput | Out-String
    foreach ($pattern in @(
            "[PASS] GitHub staging environment exists.",
            "[PASS] Repository variable STAGING_WEB_BASE_URL is configured.",
            "[PASS] Dedicated STAGING_* SSH secrets are configured.",
            "[PASS] Latest successful staging deploy found:",
            "[PASS] main requires pull requests.",
            "[PASS] main requires expected status checks:",
            "[PASS] develop blocks deletion and force pushes.",
            "Summary: 0 failed"
        )) {
        if ($readyText -notmatch [regex]::Escape($pattern)) {
            throw "Ready audit output missing '$pattern'. Output:`n$readyText"
        }
    }

    $env:SHIFTER_FAKE_GH_MODE = "missing"
    $missingOutput = & $powerShellExe @baseCommand -File $script -GhPath $fakeGh -SkipGitCheck -SkipHostedSmoke 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw "Expected missing audit to fail. Output:`n$($missingOutput | Out-String)"
    }
    $missingText = $missingOutput | Out-String
    foreach ($pattern in @(
            "[FAIL] GitHub staging environment is missing.",
            "[FAIL] Repository variable STAGING_WEB_BASE_URL is missing.",
            "[WARN] Using VPS_* SSH secrets as staging fallback",
            "[FAIL] No successful staging deploy run found on develop.",
            "[FAIL] main must require pull requests before production merges.",
            "[FAIL] main must require expected status checks before production merges:",
            "[WARN] develop does not have active no-delete/no-force-push rules.",
            "Summary:"
        )) {
        if ($missingText -notmatch [regex]::Escape($pattern)) {
            throw "Missing audit output missing '$pattern'. Output:`n$missingText"
        }
    }

    $strictMissingOutput = & $powerShellExe @baseCommand -File $script -GhPath $fakeGh -SkipGitCheck -SkipHostedSmoke -RequireDedicatedStagingSecrets 2>&1
    if ($LASTEXITCODE -eq 0) {
        throw "Expected strict missing audit to fail. Output:`n$($strictMissingOutput | Out-String)"
    }
    $strictMissingText = $strictMissingOutput | Out-String
    if ($strictMissingText -notmatch [regex]::Escape("[FAIL] Dedicated STAGING_* SSH secrets are required for the final release gate.")) {
        throw "Strict missing audit did not require dedicated staging secrets. Output:`n$strictMissingText"
    }
}
finally {
    Remove-Item Env:\SHIFTER_FAKE_GH_MODE -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Release readiness audit test passed." -ForegroundColor Green
