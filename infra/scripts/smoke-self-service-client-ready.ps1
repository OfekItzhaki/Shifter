param(
    [string]$EnvFile = "",
    [string]$WebBaseUrl = "",
    [string]$ApiBaseUrl = "",
    [string]$AdminEmail = "",
    [string]$MemberEmail = "",
    [string]$Password = "",
    [int]$TimeoutSeconds = 10,
    [switch]$SkipBrowserTest,
    [switch]$ResolveOnly
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
$webDir = Join-Path $repoRoot "apps\web"
$restoreScript = Join-Path $repoRoot "infra\scripts\restore-compose.sh"

function Read-EnvFile {
    param([string]$Path)

    $values = @{}
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $values
    }

    $resolvedPath = Resolve-Path $Path
    foreach ($line in Get-Content -LiteralPath $resolvedPath.Path) {
        if ($line -notmatch '^\s*([^#=\s]+)\s*=(.*)$') {
            continue
        }

        $key = $Matches[1].Trim()
        $value = $Matches[2]
        $commentIndex = $value.IndexOf("#")
        if ($commentIndex -ge 0) {
            $value = $value.Substring(0, $commentIndex)
        }

        $values[$key] = $value.Trim().Trim('"').Trim("'")
    }

    return $values
}

function Get-ConfigValue {
    param(
        [hashtable]$Values,
        [string[]]$Keys,
        [string]$Fallback
    )

    foreach ($key in $Keys) {
        if ($Values.ContainsKey($key) -and -not [string]::IsNullOrWhiteSpace([string]$Values[$key])) {
            return [string]$Values[$key]
        }

        $processValue = [Environment]::GetEnvironmentVariable($key)
        if (-not [string]::IsNullOrWhiteSpace($processValue)) {
            return $processValue
        }
    }

    return $Fallback
}

$envFileValues = Read-EnvFile $EnvFile

if ([string]::IsNullOrWhiteSpace($WebBaseUrl)) {
    $WebBaseUrl = Get-ConfigValue $envFileValues @("E2E_BASE_URL", "APP_FRONTEND_BASE_URL") "http://localhost:3000"
}

if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    $ApiBaseUrl = Get-ConfigValue $envFileValues @("NEXT_PUBLIC_API_URL", "APP_API_BASE_URL") "http://localhost:5000"
}

if ([string]::IsNullOrWhiteSpace($AdminEmail)) {
    $AdminEmail = Get-ConfigValue $envFileValues @("E2E_ADMIN_EMAIL") "admin@demo.local"
}

if ([string]::IsNullOrWhiteSpace($MemberEmail)) {
    $MemberEmail = Get-ConfigValue $envFileValues @("E2E_MEMBER_EMAIL") "ofek@demo.local"
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    $Password = Get-ConfigValue $envFileValues @("E2E_DEMO_PASSWORD") "Demo1234!"
}

if ($ResolveOnly) {
    Write-Host "Resolved smoke configuration:" -ForegroundColor Cyan
    Write-Host "  WebBaseUrl: $WebBaseUrl"
    Write-Host "  ApiBaseUrl: $ApiBaseUrl"
    Write-Host "  AdminEmail: $AdminEmail"
    Write-Host "  MemberEmail: $MemberEmail"
    Write-Host "  Password: $(if ([string]::IsNullOrWhiteSpace($Password)) { "not set" } else { "set" })"
    Write-Host "  SkipBrowserTest: $SkipBrowserTest"
    exit 0
}

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
        TimeoutSec = $TimeoutSeconds
        UseBasicParsing = $true
    }

    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 8)
    }

    try {
        $response = Invoke-WebRequest @params
    }
    catch {
        throw "HTTP $Method $Url failed. Confirm the API is running, migrations/seed data are loaded, and ApiBaseUrl is correct. $($_.Exception.Message)"
    }

    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return $null
    }

    return $response.Content | ConvertFrom-Json -NoEnumerate
}

function Invoke-Text {
    param(
        [ValidateSet("GET")]
        [string]$Method,
        [string]$Url,
        [string]$Token = $null
    )

    $headers = @{}
    if ($Token) {
        $headers.Authorization = "Bearer $Token"
    }

    try {
        $response = Invoke-WebRequest -Uri $Url -Method $Method -Headers $headers -TimeoutSec $TimeoutSeconds -UseBasicParsing
    }
    catch {
        throw "HTTP $Method $Url failed. Confirm the API is running, migrations/seed data are loaded, and ApiBaseUrl is correct. $($_.Exception.Message)"
    }

    return [string]$response.Content
}

function Assert-HttpOk {
    param(
        [string]$Url,
        [string]$ServiceName
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec $TimeoutSeconds -UseBasicParsing
    }
    catch {
        throw "$ServiceName check failed for $Url. Start the service or pass the correct URL. $($_.Exception.Message)"
    }

    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "$ServiceName check failed for $Url with HTTP $($response.StatusCode)."
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

function Assert-Property {
    param(
        [object]$Object,
        [string]$PropertyName,
        [string]$Context
    )

    if ($null -eq $Object) {
        throw "$Context did not return a response."
    }

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        throw "$Context response is missing '$PropertyName'."
    }
}

function Assert-NumberAtLeast {
    param(
        [object]$Object,
        [string]$PropertyName,
        [int]$Minimum,
        [string]$Context
    )

    Assert-Property $Object $PropertyName $Context
    $value = $Object.PSObject.Properties[$PropertyName].Value
    if ($null -eq $value -or [int]$value -lt $Minimum) {
        throw "$Context response expected '$PropertyName' to be at least $Minimum, got '$value'."
    }
}

function Assert-ArrayResponse {
    param(
        [object]$Value,
        [string]$Context
    )

    if ($null -eq $Value) {
        throw "$Context did not return a response."
    }

    if ($Value -is [array]) {
        return
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        return
    }

    throw "$Context should return an array response."
}

function Assert-TextContains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Context
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        throw "$Context returned empty text."
    }

    if (-not $Text.Contains($Expected, [StringComparison]::Ordinal)) {
        throw "$Context expected text containing '$Expected'."
    }
}

function Test-BashScriptSyntax {
    param([string]$ScriptPath)

    if (-not (Test-Path -LiteralPath $ScriptPath)) {
        throw "Missing restore script: $ScriptPath"
    }

    $bashCandidates = @()
    $gitBash = "C:\Program Files\Git\bin\bash.exe"
    if (Test-Path -LiteralPath $gitBash) {
        $bashCandidates += $gitBash
    }

    $bashCommand = Get-Command bash -ErrorAction SilentlyContinue
    if ($bashCommand -and $bashCandidates -notcontains $bashCommand.Source) {
        $bashCandidates += $bashCommand.Source
    }

    if ($bashCandidates.Count -eq 0) {
        Write-Warning "Bash was not found; skipping restore-compose.sh syntax check."
        return
    }

    $failures = @()
    foreach ($bashPath in $bashCandidates) {
        & $bashPath -n $ScriptPath
        if ($LASTEXITCODE -eq 0) {
            return
        }

        $failures += "$bashPath exited with $LASTEXITCODE"
    }

    throw "restore-compose.sh syntax check failed with all Bash candidates: $($failures -join '; ')."
}

Write-Step "Checking customer-hosted restore script syntax"
Test-BashScriptSyntax $restoreScript

Write-Step "Checking API health at $ApiBaseUrl"
Assert-HttpOk "$ApiBaseUrl/health" "API health"

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
foreach ($property in @(
        "allowMemberShiftClaims",
        "allowWaitlist"
    )) {
    Assert-Property $slots $property "Available slots"
}

Write-Step "Checking self-service workflow read models"
$config = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/self-service-config" -Token $adminToken
foreach ($property in @(
        "minShiftsPerCycle",
        "maxShiftsPerCycle",
        "maxAbsencesPerCycle",
        "maxLateCancellationsPerCycle",
        "allowMemberShiftClaims",
        "allowWaitlist",
        "allowShiftChangeRequests",
        "allowAbsenceReports",
        "allowShiftSwaps"
    )) {
    Assert-Property $config $property "Self-service config"
}

$myShifts = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/shift-requests/mine?schedulingCycleId=$($status.cycleId)" -Token $memberToken
foreach ($property in @(
        "requests",
        "currentShiftCount",
        "minShiftsPerCycle",
        "maxShiftsPerCycle",
        "maxLateReports",
        "lateCancellationWindowHours",
        "allowShiftChangeRequests",
        "allowAbsenceReports",
        "allowShiftSwaps"
    )) {
    Assert-Property $myShifts $property "Member shift list"
}
Assert-ArrayResponse $myShifts.requests "Member shift list requests"

$myAbsences = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/shift-requests/absence-reports/mine?cycleId=$($status.cycleId)" -Token $memberToken
foreach ($property in @(
        "reports",
        "absenceReportsUsed",
        "maxAbsenceReports",
        "lateReportsUsed",
        "maxLateReports",
        "schedulingCycleId"
    )) {
    Assert-Property $myAbsences $property "Member absence report list"
}
Assert-ArrayResponse $myAbsences.reports "Member absence report list reports"

$myChanges = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/shift-change-requests/mine" -Token $memberToken
Assert-ArrayResponse $myChanges "Member shift-change list"

$myWaitlist = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/waitlist/mine" -Token $memberToken
Assert-ArrayResponse $myWaitlist "Member waitlist"

$adminAbsences = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/shift-requests/absence-reports?status=Pending" -Token $adminToken
Assert-ArrayResponse $adminAbsences "Admin pending absence report review list"

$adminChanges = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/shift-change-requests/admin?status=Pending" -Token $adminToken
Assert-ArrayResponse $adminChanges "Admin pending shift-change review list"

$adminAssignments = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/shift-slots/admin/assignments?cycleId=$($status.cycleId)" -Token $adminToken
Assert-ArrayResponse $adminAssignments "Admin shift assignment list"

$closeout = Invoke-Json -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/self-service-cycles/closeout?cycleId=$($status.cycleId)" -Token $adminToken
foreach ($property in @(
        "cycleId",
        "slotCount",
        "approvedAssignments",
        "pendingAbsenceReports",
        "pendingChangeRequests",
        "activeWaitlistEntries",
        "allowMemberShiftClaims",
        "allowWaitlist",
        "allowShiftChangeRequests",
        "allowAbsenceReports",
        "allowShiftSwaps",
        "specialDaySlotCount",
        "noCoverageSpecialDaySlotCount",
        "underfilledSpecialDaySlotCount",
        "issueCount"
    )) {
    Assert-Property $closeout $property "Self-service closeout"
}
Assert-NumberAtLeast $closeout "specialDaySlotCount" 1 "Self-service closeout"
Assert-NumberAtLeast $closeout "underfilledSpecialDaySlotCount" 1 "Self-service closeout"

$closeoutCsv = Invoke-Text -Method GET -Url "$ApiBaseUrl/spaces/$($space.id)/groups/$($group.id)/self-service-cycles/closeout.csv?cycleId=$($status.cycleId)" -Token $adminToken
foreach ($expected in @(
        "metric,value",
        "allow_member_shift_claims,",
        "allow_waitlist,",
        "allow_shift_change_requests,",
        "allow_absence_reports,",
        "allow_shift_swaps,",
        "special_day_slot_count,",
        "no_coverage_special_day_slot_count,",
        "underfilled_special_day_slot_count,"
    )) {
    Assert-TextContains $closeoutCsv $expected "Self-service closeout CSV"
}

Write-Host "Seed smoke passed: $($space.name) / $($group.name), cycle $($status.cycleId), available slots $(@($slots.slots).Count), member shifts $(@($myShifts.requests).Count), absence reports $(@($myAbsences.reports).Count), waitlist entries $(@($myWaitlist).Count)." -ForegroundColor Green

if (-not $SkipBrowserTest) {
    Write-Step "Checking web app at $WebBaseUrl"
    Assert-HttpOk $WebBaseUrl "Web app"

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
