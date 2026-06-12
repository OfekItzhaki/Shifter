param(
    [string]$Repo = "OfekItzhaki/Shifter",
    [string]$Branch = "develop",
    [string]$GhPath = "gh",
    [switch]$SkipGitCheck,
    [switch]$SkipGitHubCheck,
    [switch]$SkipReleaseControlCheck,
    [switch]$SkipHostedSmoke,
    [string]$WebBaseUrl = "",
    [string]$ApiBaseUrl = "",
    [string[]]$RequiredStatusChecks = @(
        "API Build & Test",
        "Frontend Build",
        "Solver Lint & Test",
        "Package Preflight"
    )
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

function Test-Name {
    param(
        [object[]]$Items,
        [string]$Name
    )

    foreach ($item in $Items) {
        if ([string]$item.name -eq $Name) {
            return $true
        }
    }

    return $false
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

function Test-RequiredStatusChecks {
    param(
        [object[]]$Rules,
        [string[]]$ExpectedChecks
    )

    $statusRule = $Rules | Where-Object { [string]$_.type -eq "required_status_checks" } | Select-Object -First 1
    if ($null -eq $statusRule) {
        return $false
    }

    $configured = @()
    foreach ($check in (ConvertTo-ObjectList $statusRule.parameters.required_status_checks)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$check.context)) {
            $configured += [string]$check.context
        }
    }

    foreach ($expected in $ExpectedChecks) {
        if ($configured -notcontains $expected) {
            return $false
        }
    }

    return $true
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

Write-Host "Shifter release readiness audit" -ForegroundColor Cyan
Write-Host "Repo: $Repo"
Write-Host "Branch: $Branch"

if (-not $SkipGitCheck) {
    $currentBranch = (& git branch --show-current 2>$null).Trim()
    if ($LASTEXITCODE -eq 0 -and $currentBranch -eq $Branch) {
        Write-Check PASS "Local git branch is $Branch."
    }
    else {
        Write-Check WARN "Local git branch is '$currentBranch'; expected '$Branch'."
    }

    $status = (& git status --short 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and [string]::IsNullOrWhiteSpace($status)) {
        Write-Check PASS "Local working tree is clean."
    }
    else {
        Write-Check WARN "Local working tree is not clean."
    }
}

if (-not $SkipGitHubCheck) {
    try {
        $variables = ConvertTo-ObjectList (Invoke-GhJson @("variable", "list", "--repo", $Repo, "--json", "name"))
        $secrets = ConvertTo-ObjectList (Invoke-GhJson @("secret", "list", "--repo", $Repo, "--json", "name"))
        $environmentsResult = Invoke-GhJson @("api", "repos/$Repo/environments")
        $environments = ConvertTo-ObjectList $environmentsResult.environments
        $runs = ConvertTo-ObjectList (Invoke-GhJson @(
                "run", "list",
                "--repo", $Repo,
                "--branch", $Branch,
                "--limit", "20",
                "--json", "databaseId,workflowName,status,conclusion,headSha,createdAt,url,event"
            ))

        if (Test-Name $environments "staging") {
            Write-Check PASS "GitHub staging environment exists."
        }
        else {
            Write-Check FAIL "GitHub staging environment is missing."
        }

        foreach ($name in @("STAGING_WEB_BASE_URL", "STAGING_API_BASE_URL", "STAGING_PATH", "STAGING_COMPOSE_PROJECT_NAME")) {
            if (Test-Name $variables $name) {
                Write-Check PASS "Repository variable $name is configured."
            }
            else {
                Write-Check FAIL "Repository variable $name is missing."
            }
        }

        if (Test-Name $variables "ENABLE_STAGING_DEPLOY") {
            Write-Check PASS "Repository variable ENABLE_STAGING_DEPLOY is configured."
        }
        else {
            Write-Check WARN "Repository variable ENABLE_STAGING_DEPLOY is missing; push-triggered staging deploys stay disabled."
        }

        $hasDedicatedStagingSecrets = (Test-Name $secrets "STAGING_HOST") -and
            (Test-Name $secrets "STAGING_USER") -and
            (Test-Name $secrets "STAGING_SSH_KEY")
        $hasFallbackVpsSecrets = (Test-Name $secrets "VPS_HOST") -and
            (Test-Name $secrets "VPS_USER") -and
            (Test-Name $secrets "VPS_SSH_KEY")
        $hasFallbackHetznerSecrets = (Test-Name $secrets "HETZNER_HOST") -and
            (Test-Name $secrets "HETZNER_USER") -and
            (Test-Name $secrets "HETZNER_SSH_KEY")

        if ($hasDedicatedStagingSecrets) {
            Write-Check PASS "Dedicated STAGING_* SSH secrets are configured."
        }
        elseif ($hasFallbackVpsSecrets) {
            Write-Check WARN "Using VPS_* SSH secrets as staging fallback; prefer dedicated STAGING_* secrets for isolation."
        }
        elseif ($hasFallbackHetznerSecrets) {
            Write-Check WARN "Using HETZNER_* SSH secrets as staging fallback; prefer dedicated STAGING_* secrets for isolation."
        }
        else {
            Write-Check FAIL "No complete staging SSH secret set is configured."
        }

        $latestCi = $runs | Where-Object {
            $_.workflowName -eq "CI" -and $_.status -eq "completed" -and $_.conclusion -eq "success"
        } | Select-Object -First 1
        if ($null -ne $latestCi) {
            Write-Check PASS "Latest successful CI run found: $($latestCi.databaseId) ($($latestCi.headSha.Substring(0, 7)))."
        }
        else {
            Write-Check FAIL "No successful CI run found on $Branch."
        }

        $latestPreflight = $runs | Where-Object {
            $_.workflowName -eq "Customer-Hosted Preflight" -and $_.status -eq "completed" -and $_.conclusion -eq "success"
        } | Select-Object -First 1
        if ($null -ne $latestPreflight) {
            Write-Check PASS "Latest successful customer-hosted preflight found: $($latestPreflight.databaseId) ($($latestPreflight.headSha.Substring(0, 7)))."
        }
        else {
            Write-Check FAIL "No successful customer-hosted preflight run found on $Branch."
        }

        if (-not $SkipReleaseControlCheck) {
            $rulesetSummaries = ConvertTo-ObjectList (Invoke-GhJson @("api", "repos/$Repo/rulesets"))
            $rulesets = @()
            foreach ($summary in $rulesetSummaries) {
                $rulesets += Invoke-GhJson @("api", "repos/$Repo/rulesets/$($summary.id)")
            }

            $mainRules = Get-MatchingRules $rulesets "main" -DefaultBranch
            $developRules = Get-MatchingRules $rulesets $Branch

            if ((Test-Rule $mainRules "deletion") -and (Test-Rule $mainRules "non_fast_forward")) {
                Write-Check PASS "main blocks deletion and force pushes."
            }
            else {
                Write-Check FAIL "main must block deletion and force pushes."
            }

            if (Test-Rule $mainRules "pull_request") {
                Write-Check PASS "main requires pull requests."
            }
            else {
                Write-Check FAIL "main must require pull requests before production merges."
            }

            if (Test-RequiredStatusChecks $mainRules $RequiredStatusChecks) {
                Write-Check PASS "main requires expected status checks: $($RequiredStatusChecks -join ', ')."
            }
            else {
                Write-Check FAIL "main must require expected status checks before production merges: $($RequiredStatusChecks -join ', ')."
            }

            if ((Test-Rule $developRules "deletion") -and (Test-Rule $developRules "non_fast_forward")) {
                Write-Check PASS "$Branch blocks deletion and force pushes."
            }
            else {
                Write-Check WARN "$Branch does not have active no-delete/no-force-push rules."
            }
        }
    }
    catch {
        Write-Check FAIL $_.Exception.Message
    }
}

if (-not $SkipHostedSmoke) {
    if ([string]::IsNullOrWhiteSpace($WebBaseUrl) -or [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
        Write-Check WARN "Hosted smoke skipped because WebBaseUrl and ApiBaseUrl were not provided."
    }
    else {
        try {
            & (Join-Path $PSScriptRoot "smoke-hosted-vps.ps1") -WebBaseUrl $WebBaseUrl -ApiBaseUrl $ApiBaseUrl
            if ($LASTEXITCODE -eq 0) {
                Write-Check PASS "Hosted smoke passed for supplied URLs."
            }
            else {
                Write-Check FAIL "Hosted smoke failed for supplied URLs."
            }
        }
        catch {
            Write-Check FAIL "Hosted smoke failed. $($_.Exception.Message)"
        }
    }
}

Write-Host ""
Write-Host "Summary: $failed failed, $warned warnings." -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
}

exit 0
