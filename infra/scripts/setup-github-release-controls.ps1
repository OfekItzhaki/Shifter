param(
    [string]$Repo = "OfekItzhaki/Shifter",
    [string]$GhPath = "gh",
    [string]$MainRulesetName = "Main",
    [string]$DevelopRulesetName = "Develop",
    [string[]]$RequiredStatusChecks = @(
        "API Build & Test",
        "Frontend Build",
        "Solver Lint & Test",
        "Package Preflight"
    ),
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

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

function Invoke-Gh {
    param([string[]]$Arguments)

    if (-not $Apply) {
        Write-Host "DRY-RUN gh $($Arguments -join ' ')" -ForegroundColor DarkGray
        return
    }

    $output = & $GhPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Arguments -join ' ') failed with exit $LASTEXITCODE. Output:`n$($output | Out-String)"
    }
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

function Find-Ruleset {
    param(
        [object[]]$Rulesets,
        [string]$Name
    )

    foreach ($ruleset in $Rulesets) {
        if ([string]$ruleset.name -eq $Name) {
            return $ruleset
        }
    }

    return $null
}

function New-StatusCheckRule {
    param([string[]]$Checks)

    $required = @()
    foreach ($check in $Checks) {
        if (-not [string]::IsNullOrWhiteSpace($check)) {
            $required += [ordered]@{
                context = $check
            }
        }
    }

    if ($required.Count -eq 0) {
        throw "At least one required status check is required."
    }

    return [ordered]@{
        type = "required_status_checks"
        parameters = [ordered]@{
            strict_required_status_checks_policy = $true
            required_status_checks = $required
        }
    }
}

function New-RulesetPayload {
    param(
        [string]$Name,
        [string[]]$Includes,
        [object[]]$Rules
    )

    return [ordered]@{
        name = $Name
        target = "branch"
        enforcement = "active"
        bypass_actors = @()
        conditions = [ordered]@{
            ref_name = [ordered]@{
                include = $Includes
                exclude = @()
            }
        }
        rules = $Rules
    }
}

function Write-Payload {
    param(
        [string]$Path,
        [object]$Payload
    )

    $Payload | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding ASCII
}

if ([string]::IsNullOrWhiteSpace($Repo)) {
    throw "Repo is required."
}

$nonBlankChecks = @($RequiredStatusChecks | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($nonBlankChecks.Count -eq 0) {
    throw "RequiredStatusChecks must include at least one check name."
}

Write-Host "Shifter GitHub release controls setup" -ForegroundColor Cyan
Write-Host "Repo: $Repo"
Write-Host "Apply: $Apply"
Write-Host "Required status checks: $($nonBlankChecks -join ', ')"

$rulesetSummaries = ConvertTo-ObjectList (Invoke-GhJson @("api", "repos/$Repo/rulesets"))
$mainSummary = Find-Ruleset $rulesetSummaries $MainRulesetName
$developSummary = Find-Ruleset $rulesetSummaries $DevelopRulesetName

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("shifter-release-controls-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    $mainPayload = New-RulesetPayload `
        -Name $MainRulesetName `
        -Includes @("~DEFAULT_BRANCH") `
        -Rules @(
            [ordered]@{ type = "deletion" },
            [ordered]@{ type = "non_fast_forward" },
            [ordered]@{
                type = "pull_request"
                parameters = [ordered]@{
                    allowed_merge_methods = @("merge", "squash", "rebase")
                    dismiss_stale_reviews_on_push = $true
                    require_code_owner_review = $false
                    require_last_push_approval = $false
                    required_approving_review_count = 0
                    required_review_thread_resolution = $true
                }
            },
            (New-StatusCheckRule $nonBlankChecks)
        )

    $developPayload = New-RulesetPayload `
        -Name $DevelopRulesetName `
        -Includes @("refs/heads/develop") `
        -Rules @(
            [ordered]@{ type = "deletion" },
            [ordered]@{ type = "non_fast_forward" }
        )

    $mainPayloadPath = Join-Path $tempDir "main-ruleset.json"
    $developPayloadPath = Join-Path $tempDir "develop-ruleset.json"
    Write-Payload $mainPayloadPath $mainPayload
    Write-Payload $developPayloadPath $developPayload

    if ($null -ne $mainSummary) {
        Invoke-Gh @("api", "-X", "PUT", "repos/$Repo/rulesets/$($mainSummary.id)", "--input", $mainPayloadPath)
    }
    else {
        Invoke-Gh @("api", "-X", "POST", "repos/$Repo/rulesets", "--input", $mainPayloadPath)
    }

    if ($null -ne $developSummary) {
        Invoke-Gh @("api", "-X", "PUT", "repos/$Repo/rulesets/$($developSummary.id)", "--input", $developPayloadPath)
    }
    else {
        Invoke-Gh @("api", "-X", "POST", "repos/$Repo/rulesets", "--input", $developPayloadPath)
    }
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
if ($Apply) {
    Write-Host "GitHub release controls configured." -ForegroundColor Green
}
else {
    Write-Host "Dry run only. Re-run with -Apply to write GitHub release controls." -ForegroundColor Yellow
}
