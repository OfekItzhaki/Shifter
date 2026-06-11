param(
    [string]$WebBaseUrl = $(if ($env:E2E_BASE_URL) { $env:E2E_BASE_URL } else { "http://localhost:3000" }),
    [string]$ApiBaseUrl = $(if ($env:NEXT_PUBLIC_API_URL) { $env:NEXT_PUBLIC_API_URL } else { "http://localhost:5000" }),
    [string]$AdminEmail = $(if ($env:E2E_ADMIN_EMAIL) { $env:E2E_ADMIN_EMAIL } else { "admin@demo.local" }),
    [string]$MemberEmail = $(if ($env:E2E_MEMBER_EMAIL) { $env:E2E_MEMBER_EMAIL } else { "ofek@demo.local" }),
    [string]$Password = $(if ($env:E2E_DEMO_PASSWORD) { $env:E2E_DEMO_PASSWORD } else { "Demo1234!" }),
    [switch]$SkipBrowserTest
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
$webDir = Join-Path $repoRoot "apps\web"

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Json {
    param(
        [ValidateSet("GET", "POST")]
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [string]$Token = $null
    )

    $headers = @{}
    if ($Token) {
        $headers.Authorization = "Bearer $Token"
    }

    $params = @{
        Uri = $Url
        Method = $Method
        Headers = $headers
        TimeoutSec = 20
        UseBasicParsing = $true
    }

    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 8)
    }

    $response = Invoke-WebRequest @params
    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return $null
    }

    return $response.Content | ConvertFrom-Json
}

function Assert-HttpOk {
    param([string]$Url)

    $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 20 -UseBasicParsing
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "$Url returned HTTP $($response.StatusCode)"
    }
}

function Login {
    param([string]$Email)

    $result = Invoke-Json -Method POST -Url "$ApiBaseUrl/auth/login" -Body @{
        identifier = $Email
        password = $Password
    }

    if (-not $result.accessToken) {
        throw "Login for $Email did not return accessToken. Check seed data and password."
    }

    return $result.accessToken
}

Write-Step "Checking API health at $ApiBaseUrl"
Assert-HttpOk "$ApiBaseUrl/health"

Write-Step "Checking seeded demo users"
$adminToken = Login $AdminEmail
$memberToken = Login $MemberEmail

Write-Step "Checking seeded self-service group"
$spaces = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces" -Token $adminToken
$space = @($spaces | Where-Object { $_.name -eq "Unit Alpha" } | Select-Object -First 1)
if (-not $space) {
    $space = @($spaces | Select-Object -First 1)
}
if (-not $space) {
    throw "No spaces found. Load infra/scripts/seed.sql before running this smoke test."
}

$groups = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups" -Token $adminToken
$group = @($groups | Where-Object { $_.name -eq "Self-Service Demo" -and $_.schedulingMode -eq "SelfService" } | Select-Object -First 1)
if (-not $group) {
    throw "Could not find seeded Self-Service Demo group in space $($space.name)."
}

$status = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/self-service-cycles/status" -Token $adminToken
if (-not $status.cycleId) {
    throw "Self-Service Demo has no active cycle."
}

$slots = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/shift-slots/available?cycleId=$($status.cycleId)" -Token $memberToken
if (-not $slots.slots -or @($slots.slots).Count -eq 0) {
    throw "Self-Service Demo has no available member slots for $MemberEmail."
}

Write-Host "Seed smoke passed: $($space.name) / $($group.name), cycle $($status.cycleId), available slots $(@($slots.slots).Count)." -ForegroundColor Green

if (-not $SkipBrowserTest) {
    Write-Step "Checking web app at $WebBaseUrl"
    Assert-HttpOk $WebBaseUrl

    Write-Step "Running special-day browser label flow"
    Push-Location $webDir
    try {
        $env:E2E_BASE_URL = $WebBaseUrl
        $env:NEXT_PUBLIC_API_URL = $ApiBaseUrl
        $env:E2E_ADMIN_EMAIL = $AdminEmail
        $env:E2E_MEMBER_EMAIL = $MemberEmail
        $env:E2E_DEMO_PASSWORD = $Password
        & .\node_modules\.bin\playwright.cmd test self-service.browser.spec.ts -g "member sees special-day labels on available shifts"
        if ($LASTEXITCODE -ne 0) {
            throw "Playwright special-day browser smoke failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "Skipped browser flow. Re-run without -SkipBrowserTest to verify the special-day picker UI." -ForegroundColor Yellow
}
