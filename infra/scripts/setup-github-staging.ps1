param(
    [string]$Repo = "OfekItzhaki/Shifter",
    [string]$EnvironmentName = "staging",
    [string]$GhPath = "gh",
    [string]$WebBaseUrl = "",
    [string]$ApiBaseUrl = "",
    [string]$StagingPath = "/opt/shifter-staging",
    [string]$ComposeProjectName = "shifter-staging",
    [switch]$EnablePushDeploy,
    [switch]$BootstrapOnly,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

function Assert-AbsoluteUrl {
    param(
        [string]$Name,
        [string]$Value
    )

    $uri = $null
    if (-not [System.Uri]::TryCreate($Value, [System.UriKind]::Absolute, [ref]$uri)) {
        throw "$Name must be an absolute URL."
    }

    if ($uri.Scheme -notin @("http", "https")) {
        throw "$Name must use http or https."
    }
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

if ([string]::IsNullOrWhiteSpace($Repo)) {
    throw "Repo is required."
}

if ([string]::IsNullOrWhiteSpace($EnvironmentName)) {
    throw "EnvironmentName is required."
}

if ($BootstrapOnly -and $EnablePushDeploy) {
    throw "BootstrapOnly cannot be used with EnablePushDeploy."
}

if (-not $BootstrapOnly -and [string]::IsNullOrWhiteSpace($WebBaseUrl)) {
    throw "WebBaseUrl is required."
}

if (-not $BootstrapOnly -and [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    throw "ApiBaseUrl is required."
}

if ([string]::IsNullOrWhiteSpace($StagingPath)) {
    throw "StagingPath is required."
}

if ([string]::IsNullOrWhiteSpace($ComposeProjectName)) {
    throw "ComposeProjectName is required."
}

if (-not $BootstrapOnly) {
    Assert-AbsoluteUrl "WebBaseUrl" $WebBaseUrl
    Assert-AbsoluteUrl "ApiBaseUrl" $ApiBaseUrl
}

$pushDeployValue = if ($EnablePushDeploy) { "true" } else { "false" }

Write-Host "Shifter GitHub staging setup" -ForegroundColor Cyan
Write-Host "Repo: $Repo"
Write-Host "Environment: $EnvironmentName"
Write-Host "Bootstrap only: $BootstrapOnly"
Write-Host "Apply: $Apply"

Invoke-Gh @("api", "-X", "PUT", "repos/$Repo/environments/$EnvironmentName")

$variables = [ordered]@{
    "ENABLE_STAGING_DEPLOY" = $pushDeployValue
    "STAGING_PATH" = $StagingPath
    "STAGING_COMPOSE_PROJECT_NAME" = $ComposeProjectName
}

if (-not $BootstrapOnly) {
    $variables["STAGING_WEB_BASE_URL"] = $WebBaseUrl
    $variables["STAGING_API_BASE_URL"] = $ApiBaseUrl
}

foreach ($entry in $variables.GetEnumerator()) {
    Invoke-Gh @("variable", "set", $entry.Key, "--repo", $Repo, "--body", $entry.Value)
}

Write-Host ""
if ($Apply) {
    if ($BootstrapOnly) {
        Write-Host "GitHub staging environment and bootstrap variables configured." -ForegroundColor Green
    }
    else {
        Write-Host "GitHub staging environment and variables configured." -ForegroundColor Green
    }
}
else {
    Write-Host "Dry run only. Re-run with -Apply to write GitHub configuration." -ForegroundColor Yellow
}

if ($BootstrapOnly) {
    Write-Host ""
    Write-Host "Bootstrap mode intentionally skipped STAGING_WEB_BASE_URL and STAGING_API_BASE_URL." -ForegroundColor Yellow
    Write-Host "Run this script again without -BootstrapOnly when staging URLs are ready."
    Write-Host ""
    Write-Host "Next full setup command:" -ForegroundColor Cyan
    Write-Host ".\infra\scripts\setup-github-staging.ps1 ``"
    Write-Host "  -WebBaseUrl <staging-web-url> ``"
    Write-Host "  -ApiBaseUrl <staging-api-url> ``"
    Write-Host "  -StagingPath $StagingPath ``"
    Write-Host "  -ComposeProjectName $ComposeProjectName ``"
    Write-Host "  -Apply"
    Write-Host ""
    Write-Host "Keep push deploy disabled until the first staging deploy and smoke pass; add -EnablePushDeploy only after that."
}

Write-Host ""
Write-Host "Secrets still need to be set manually or with gh secret set:" -ForegroundColor Cyan
Write-Host "  gh secret set STAGING_HOST --repo $Repo"
Write-Host "  gh secret set STAGING_USER --repo $Repo"
Write-Host "  gh secret set STAGING_SSH_KEY --repo $Repo"
Write-Host "  gh secret set STAGING_PORT --repo $Repo   # optional"
