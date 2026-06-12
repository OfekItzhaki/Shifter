param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$EnvFile = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $ShifterDir "infra\compose\.env"
}

if (-not (Test-Path -LiteralPath $EnvFile)) {
    Write-Error "Missing env file: $EnvFile. Copy infra/compose/.env.customer.example to infra/compose/.env first."
    exit 1
}

$envValues = @{}
foreach ($line in Get-Content -LiteralPath $EnvFile) {
    if ($line -notmatch '^\s*([^#=\s]+)\s*=(.*)$') {
        continue
    }

    $key = $Matches[1].Trim()
    $value = $Matches[2]
    $commentIndex = $value.IndexOf("#")
    if ($commentIndex -ge 0) {
        $value = $value.Substring(0, $commentIndex)
    }
    $envValues[$key] = $value.Trim()
}

$errors = 0
$warnings = 0

function Get-EnvValue {
    param([string]$Key)
    if ($envValues.ContainsKey($Key)) {
        return [string]$envValues[$Key]
    }
    return ""
}

function Add-Error {
    param([string]$Message)
    [Console]::Error.WriteLine("ERROR: $Message")
    $script:errors++
}

function Add-WarningMessage {
    param([string]$Message)
    Write-Warning $Message
    $script:warnings++
}

function Require-Value {
    param([string]$Key)

    $value = Get-EnvValue $Key
    if ([string]::IsNullOrWhiteSpace($value)) {
        Add-Error "$Key is required."
        return
    }

    if ($value -match '^(change_me|changeme|your-)' -or $value -match 'customer\.example|example\.com') {
        Add-Error "$Key still looks like a placeholder: $value"
    }
}

function Warn-If-Set {
    param(
        [string]$Key,
        [string]$Reason
    )

    if (-not [string]::IsNullOrWhiteSpace((Get-EnvValue $Key))) {
        Add-WarningMessage "$Key is set. $Reason"
    }
}

function Warn-If-Empty {
    param(
        [string]$Key,
        [string]$Reason
    )

    if ([string]::IsNullOrWhiteSpace((Get-EnvValue $Key))) {
        Add-WarningMessage "$Key is empty. $Reason"
    }
}

$mode = Get-EnvValue "SHIFTER_DEPLOYMENT_MODE"
if ($mode -ne "customer-hosted") {
    Add-Error "SHIFTER_DEPLOYMENT_MODE must be customer-hosted for this validator."
}

$requiredKeys = @(
    "POSTGRES_DB",
    "POSTGRES_USER",
    "POSTGRES_PASSWORD",
    "REDIS_PASSWORD",
    "API_PORT",
    "WEB_PORT",
    "JWT_SECRET",
    "JWT_ISSUER",
    "JWT_AUDIENCE",
    "FIELD_ENCRYPTION_KEY",
    "SOLVER_TIMEOUT_SECONDS",
    "APP_FRONTEND_BASE_URL",
    "APP_API_BASE_URL",
    "NEXT_PUBLIC_API_URL",
    "NEXT_PUBLIC_LEGAL_EMAIL",
    "MINIO_ROOT_USER",
    "MINIO_ROOT_PASSWORD",
    "SEQ_ADMIN_PASSWORD"
)

foreach ($key in $requiredKeys) {
    Require-Value $key
}

$jwtSecret = Get-EnvValue "JWT_SECRET"
if (-not [string]::IsNullOrWhiteSpace($jwtSecret) -and $jwtSecret.Length -lt 32) {
    Add-Error "JWT_SECRET must be at least 32 characters."
}

$fieldEncryptionKey = Get-EnvValue "FIELD_ENCRYPTION_KEY"
if (-not [string]::IsNullOrWhiteSpace($fieldEncryptionKey) -and $fieldEncryptionKey.Length -lt 32) {
    Add-Error "FIELD_ENCRYPTION_KEY must be at least 32 characters."
}

$apiUrl = Get-EnvValue "APP_API_BASE_URL"
$publicApiUrl = Get-EnvValue "NEXT_PUBLIC_API_URL"
foreach ($urlName in @("APP_FRONTEND_BASE_URL", "APP_API_BASE_URL", "NEXT_PUBLIC_API_URL")) {
    $urlValue = Get-EnvValue $urlName
    if (-not [string]::IsNullOrWhiteSpace($urlValue) -and $urlValue -notmatch '^https://') {
        Add-WarningMessage "$urlName should be HTTPS in production: $urlValue"
    }
}

if (-not [string]::IsNullOrWhiteSpace($apiUrl) -and -not [string]::IsNullOrWhiteSpace($publicApiUrl) -and $apiUrl -ne $publicApiUrl) {
    Add-WarningMessage "APP_API_BASE_URL and NEXT_PUBLIC_API_URL differ. This is valid only if intended."
}

$aiKey = Get-EnvValue "AI_API_KEY"
$aiBaseUrl = Get-EnvValue "AI_BASE_URL"
$aiModel = Get-EnvValue "AI_MODEL"
$aiNoExport = Get-EnvValue "AI_NO_EXPORT_REQUIRED"
if (-not [string]::IsNullOrWhiteSpace($aiKey) -and [string]::IsNullOrWhiteSpace($aiBaseUrl)) {
    Add-WarningMessage "AI_API_KEY is set but AI_BASE_URL is empty; the API will use OpenAI's default endpoint."
}
if (-not [string]::IsNullOrWhiteSpace($aiBaseUrl) -and [string]::IsNullOrWhiteSpace($aiModel)) {
    Add-Error "AI_MODEL is required when AI_BASE_URL is set."
}
if ([string]::IsNullOrWhiteSpace($aiKey) -and [string]::IsNullOrWhiteSpace($aiBaseUrl)) {
    Add-WarningMessage "AI is disabled. Schedule solving still works, but AI chat/import features will not."
}
if ($aiNoExport -eq "true") {
    if ([string]::IsNullOrWhiteSpace($aiBaseUrl)) {
        Add-Error "AI_NO_EXPORT_REQUIRED=true requires AI_BASE_URL to point to a private/local OpenAI-compatible endpoint."
    }
    elseif ($aiBaseUrl -notmatch '^(http://(localhost|127\.|10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[0-1])\.|[^/]*\.(internal|local))|https://[^/]*\.(internal|local))') {
        Add-Error "AI_NO_EXPORT_REQUIRED=true requires AI_BASE_URL to use localhost, a private IP, .internal, or .local endpoint: $aiBaseUrl"
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($aiNoExport) -and $aiNoExport -ne "false") {
    Add-Error "AI_NO_EXPORT_REQUIRED must be true, false, or empty."
}

$storageBucket = Get-EnvValue "STORAGE_S3_BUCKET_NAME"
$storageServiceUrl = Get-EnvValue "STORAGE_S3_SERVICE_URL"
if (-not [string]::IsNullOrWhiteSpace($storageBucket)) {
    Require-Value "STORAGE_S3_ACCESS_KEY"
    Require-Value "STORAGE_S3_SECRET_KEY"
    if ($storageServiceUrl -match 'minio:9000' -and (Get-EnvValue "STORAGE_S3_FORCE_PATH_STYLE") -ne "true") {
        Add-Error "STORAGE_S3_FORCE_PATH_STYLE must be true for bundled MinIO."
    }
}
else {
    Add-WarningMessage "STORAGE_S3_BUCKET_NAME is empty; uploads will use local disk storage."
}

Warn-If-Empty "RESEND_API_KEY" "Email delivery will be logged only; password reset and invitations may not reach users."
Warn-If-Set "NEXT_PUBLIC_POSTHOG_KEY" "Analytics may send usage data outside the customer environment."
Warn-If-Set "NEXT_PUBLIC_SENTRY_DSN" "Frontend errors may be sent outside the customer environment."
Warn-If-Set "NEXT_PUBLIC_CRISP_WEBSITE_ID" "Chat widget data may be sent outside the customer environment."
Warn-If-Set "LEMONSQUEEZY_API_KEY" "Billing calls may leave the customer environment."

if ($errors -gt 0) {
    [Console]::Error.WriteLine("Validation failed: $errors error(s), $warnings warning(s).")
    exit 1
}

Write-Host "Validation passed: $warnings warning(s)." -ForegroundColor Green
