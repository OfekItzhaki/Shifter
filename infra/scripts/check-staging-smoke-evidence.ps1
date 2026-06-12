param(
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath
)

$ErrorActionPreference = "Stop"

function Write-Check {
    param(
        [ValidateSet("PASS", "FAIL")]
        [string]$Status,
        [string]$Message
    )

    $color = if ($Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "[$Status] $Message" -ForegroundColor $color

    if ($Status -eq "FAIL") {
        $script:failed++
    }
}

function Test-Field {
    param(
        [string]$Text,
        [string]$Label
    )

    $escapedLabel = [regex]::Escape($Label)
    $match = [regex]::Match($Text, "(?m)^- $escapedLabel[^\S\r\n]*:[^\S\r\n]*(.*)$")
    if (-not $match.Success) {
        Write-Check FAIL "Missing '$Label' field."
        return
    }

    $value = $match.Groups[1].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($value) -or $value -match "^<.*>$") {
        Write-Check FAIL "'$Label' is not filled in."
    }
    else {
        Write-Check PASS "'$Label' is filled in."
    }
}

$failed = 0
$resolvedPath = (Resolve-Path $EvidencePath).Path
$text = Get-Content -LiteralPath $resolvedPath -Raw

Write-Host "Staging smoke evidence check" -ForegroundColor Cyan
Write-Host "Evidence: $resolvedPath"

foreach ($label in @(
        "Test date",
        "Tester",
        "Source commit",
        "Staging web URL",
        "Staging API URL",
        'GitHub `Deploy Staging` run',
        "Customer-hosted preflight run",
        "Broad CI run",
        "Release readiness audit result",
        "Hosted smoke command/result",
        "Admin test account",
        "Member test account",
        "Browser/device matrix",
        "Accepted by",
        "Accepted at"
    )) {
    Test-Field $text $label
}

if ($text -match "\|\s*pending\s*\|") {
    Write-Check FAIL "Evidence still contains pending checklist rows."
}
else {
    Write-Check PASS "No pending checklist rows remain."
}

if ($text -match '(?m)^- Staging accepted for `develop` to `main` PR:\s*yes\s*$') {
    Write-Check PASS "Staging sign-off is accepted."
}
else {
    Write-Check FAIL "Staging sign-off is not marked yes."
}

if ($text -match "(?i)\b(password|token|bearer|secret|connection string)\s*[:=]\s*\S+") {
    Write-Check FAIL "Evidence may contain secret-like material. Remove sensitive values before committing or sharing."
}
else {
    Write-Check PASS "No obvious secret-like material found."
}

Write-Host ""
Write-Host "Summary: $failed failed." -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
}

exit 0
