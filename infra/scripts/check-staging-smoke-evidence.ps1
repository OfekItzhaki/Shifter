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
        return $null
    }

    $value = $match.Groups[1].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($value) -or $value -match "^<.*>$") {
        Write-Check FAIL "'$Label' is not filled in."
        return $null
    }
    else {
        Write-Check PASS "'$Label' is filled in."
        return $value
    }
}

function Test-FieldEquals {
    param(
        [string]$Text,
        [string]$Label,
        [string]$Expected
    )

    $value = Test-Field $Text $Label
    if ($null -eq $value) {
        return
    }

    if ($value -eq $Expected) {
        Write-Check PASS "'$Label' is '$Expected'."
    }
    else {
        Write-Check FAIL "'$Label' must be '$Expected', got '$value'."
    }
}

function Test-YesNoField {
    param(
        [string]$Text,
        [string]$Label
    )

    $value = Test-Field $Text $Label
    if ($null -eq $value) {
        return
    }

    if ($value -in @("yes", "no")) {
        Write-Check PASS "'$Label' is explicitly '$value'."
    }
    else {
        Write-Check FAIL "'$Label' must be either 'yes' or 'no', got '$value'."
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
        "Space",
        "Group",
        "Self-service cycle",
        "Email inbox or provider used for reset/invite testing",
        "Browser/device matrix",
        "Accepted by",
        "Accepted at"
    )) {
    Test-Field $text $label
}

Test-FieldEquals $text "Source branch" '`develop`'
Test-YesNoField $text "Special-day test data present"

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
