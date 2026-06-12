param(
    [string]$Repo = "OfekItzhaki/Shifter",
    [string]$GhPath = "gh",
    [string]$MainBranch = "main",
    [string]$DevelopBranch = "develop"
)

$ErrorActionPreference = "Stop"

$failed = 0
$warned = 0

function Write-Check {
    param(
        [ValidateSet("PASS", "WARN", "FAIL")]
        [string]$Status,
        [string]$Message
    )

    $color = switch ($Status) {
        "PASS" { "Green" }
        "WARN" { "Yellow" }
        "FAIL" { "Red" }
    }

    Write-Host "[$Status] $Message" -ForegroundColor $color

    if ($Status -eq "FAIL") {
        $script:failed++
    }
    elseif ($Status -eq "WARN") {
        $script:warned++
    }
}

function Invoke-GhJson {
    param([string[]]$Arguments)

    $output = & $GhPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed with exit $LASTEXITCODE. Output:`n$($output | Out-String)"
    }

    $text = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    return $text | ConvertFrom-Json
}

function ConvertTo-ObjectList {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Array]) {
        return @($Value)
    }

    return @($Value)
}

function Test-Rule {
    param(
        [object[]]$Rules,
        [string]$Type
    )

    foreach ($rule in $Rules) {
        if ([string]$rule.type -eq $Type) {
            return $true
        }
    }

    return $false
}

function Test-RulesetMatchesBranch {
    param(
        [object]$Ruleset,
        [string]$Branch,
        [switch]$DefaultBranch
    )

    $includes = ConvertTo-ObjectList $Ruleset.conditions.ref_name.include
    foreach ($include in $includes) {
        $pattern = [string]$include
        if ($pattern -eq "refs/heads/$Branch" -or $pattern -eq $Branch) {
            return $true
        }

        if ($DefaultBranch -and $pattern -eq "~DEFAULT_BRANCH") {
            return $true
        }
    }

    return $false
}

function Get-MatchingRules {
    param(
        [object[]]$Rulesets,
        [string]$Branch,
        [switch]$DefaultBranch
    )

    $rules = @()
    foreach ($ruleset in $Rulesets) {
        if ([string]$ruleset.enforcement -ne "active") {
            continue
        }

        if (Test-RulesetMatchesBranch $ruleset $Branch -DefaultBranch:$DefaultBranch) {
            $rules += ConvertTo-ObjectList $ruleset.rules
        }
    }

    return $rules
}

Write-Host "GitHub release control audit" -ForegroundColor Cyan
Write-Host "Repo: $Repo"

try {
    $summaries = ConvertTo-ObjectList (Invoke-GhJson @("api", "repos/$Repo/rulesets"))
    $rulesets = @()
    foreach ($summary in $summaries) {
        $rulesets += Invoke-GhJson @("api", "repos/$Repo/rulesets/$($summary.id)")
    }

    $mainRules = Get-MatchingRules $rulesets $MainBranch -DefaultBranch
    $developRules = Get-MatchingRules $rulesets $DevelopBranch

    if ((Test-Rule $mainRules "deletion") -and (Test-Rule $mainRules "non_fast_forward")) {
        Write-Check PASS "$MainBranch blocks deletion and force pushes."
    }
    else {
        Write-Check FAIL "$MainBranch must block deletion and force pushes."
    }

    if (Test-Rule $mainRules "pull_request") {
        Write-Check PASS "$MainBranch requires pull requests."
    }
    else {
        Write-Check FAIL "$MainBranch must require pull requests before production merges."
    }

    if (Test-Rule $mainRules "required_status_checks") {
        Write-Check PASS "$MainBranch requires status checks."
    }
    else {
        Write-Check FAIL "$MainBranch must require status checks before production merges."
    }

    if ((Test-Rule $developRules "deletion") -and (Test-Rule $developRules "non_fast_forward")) {
        Write-Check PASS "$DevelopBranch blocks deletion and force pushes."
    }
    else {
        Write-Check WARN "$DevelopBranch does not have active no-delete/no-force-push rules."
    }
}
catch {
    Write-Check FAIL $_.Exception.Message
}

Write-Host ""
Write-Host "Summary: $failed failed, $warned warnings." -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
}

exit 0
