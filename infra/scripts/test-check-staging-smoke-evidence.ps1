param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $ShifterDir).Path
$script = Join-Path $root "infra\scripts\check-staging-smoke-evidence.ps1"
$template = Join-Path $root "docs\STAGING-MANUAL-SMOKE-EVIDENCE.md"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shifter-staging-evidence-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $goodPath = Join-Path $tempDir "staging-smoke-good.md"
    $badPath = Join-Path $tempDir "staging-smoke-bad.md"

    $good = Get-Content -LiteralPath $template -Raw
    $replacements = [ordered]@{
        "- Test date:" = "- Test date: 2026-06-13"
        "- Tester:" = "- Tester: Ofek"
        "- Source commit:" = "- Source commit: abcdef1"
        "- Staging web URL:" = "- Staging web URL: https://staging.example.com"
        "- Staging API URL:" = "- Staging API URL: https://staging-api.example.com"
        '- GitHub `Deploy Staging` run:' = '- GitHub `Deploy Staging` run: 123'
        "- Customer-hosted preflight run:" = "- Customer-hosted preflight run: 456"
        "- Broad CI run:" = "- Broad CI run: 789"
        "- Release readiness audit result:" = "- Release readiness audit result: passed"
        "- Hosted smoke command/result:" = "- Hosted smoke command/result: passed"
        "- Admin test account:" = "- Admin test account: admin@example.invalid"
        "- Member test account:" = "- Member test account: member@example.invalid"
        "- Browser/device matrix:" = "- Browser/device matrix: Chrome desktop, Safari mobile"
        '- Staging accepted for `develop` to `main` PR: yes / no' = '- Staging accepted for `develop` to `main` PR: yes'
        "- Accepted by:" = "- Accepted by: Ofek"
        "- Accepted at:" = "- Accepted at: 2026-06-13T12:00:00+03:00"
    }

    foreach ($entry in $replacements.GetEnumerator()) {
        $good = $good.Replace($entry.Key, $entry.Value)
    }
    $good = $good -replace "\|\s*pending\s*\|", "| passed |"
    Set-Content -LiteralPath $goodPath -Value $good -Encoding ASCII

    $bad = Get-Content -LiteralPath $template -Raw
    Set-Content -LiteralPath $badPath -Value $bad -Encoding ASCII

    $goodOutput = & $script -EvidencePath $goodPath *>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Expected completed evidence to pass. Output:`n$($goodOutput | Out-String)"
    }
    if (($goodOutput | Out-String) -notmatch [regex]::Escape("Summary: 0 failed.")) {
        throw "Completed evidence output did not show success. Output:`n$($goodOutput | Out-String)"
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $badOutput = & $script -EvidencePath $badPath *>&1
        $badExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($badExitCode -eq 0) {
        throw "Expected blank template evidence to fail. Output:`n$($badOutput | Out-String)"
    }
    foreach ($pattern in @(
            "'Test date' is not filled in.",
            "Evidence still contains pending checklist rows.",
            "Staging sign-off is not marked yes."
        )) {
        if (($badOutput | Out-String) -notmatch [regex]::Escape($pattern)) {
            throw "Blank evidence output missing '$pattern'. Output:`n$($badOutput | Out-String)"
        }
    }
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Staging smoke evidence check test passed." -ForegroundColor Green
