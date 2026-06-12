param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = ""
)

$ErrorActionPreference = "Stop"

$validatorPs = Join-Path $PSScriptRoot "validate-customer-env.ps1"
$validatorSh = Join-Path $PSScriptRoot "validate-customer-env.sh"

function New-BaseEnv {
    return [ordered]@{
        SHIFTER_DEPLOYMENT_MODE = "customer-hosted"
        POSTGRES_DB = "shifter"
        POSTGRES_USER = "shifter"
        POSTGRES_PASSWORD = "valid-postgres-password"
        REDIS_PASSWORD = "valid-redis-password"
        API_PORT = "5000"
        WEB_PORT = "3000"
        SHIFTER_LICENSEE = "Acme Scheduling Ltd"
        SHIFTER_LICENSE_KEY = "valid-customer-license-key-2026"
        JWT_SECRET = "valid-jwt-secret-minimum-32-chars"
        JWT_ISSUER = "shifter-customer-api"
        JWT_AUDIENCE = "shifter-customer-web"
        FIELD_ENCRYPTION_KEY = "valid-field-encryption-key-32chars"
        SOLVER_TIMEOUT_SECONDS = "60"
        APP_FRONTEND_BASE_URL = "https://shifter.acme.local"
        APP_API_BASE_URL = "https://api.shifter.acme.local"
        NEXT_PUBLIC_API_URL = "https://api.shifter.acme.local"
        NEXT_PUBLIC_LEGAL_EMAIL = "support@acme.test"
        MINIO_ROOT_USER = "shifter-minio"
        MINIO_ROOT_PASSWORD = "valid-minio-password"
        SEQ_ADMIN_PASSWORD = "valid-seq-password"
        STORAGE_S3_BUCKET_NAME = "shifter-uploads"
        STORAGE_S3_ACCESS_KEY = "shifter-minio"
        STORAGE_S3_SECRET_KEY = "valid-minio-password"
        STORAGE_S3_SERVICE_URL = "http://minio:9000"
        STORAGE_S3_FORCE_PATH_STYLE = "true"
        AI_NO_EXPORT_REQUIRED = "false"
        AI_API_KEY = ""
        AI_BASE_URL = ""
        AI_MODEL = ""
        RESEND_API_KEY = ""
        RESEND_FROM_EMAIL = ""
        RESEND_FROM_NAME = ""
        TWILIO_ACCOUNT_SID = ""
        TWILIO_AUTH_TOKEN = ""
        TWILIO_WHATSAPP_FROM = ""
        VAPID_PUBLIC_KEY = ""
        VAPID_PRIVATE_KEY = ""
        VAPID_SUBJECT = ""
        NEXT_PUBLIC_VAPID_PUBLIC_KEY = ""
        PUSHOVER_USER_KEY = ""
        PUSHOVER_APP_TOKEN = ""
        NEXT_PUBLIC_POSTHOG_KEY = ""
        NEXT_PUBLIC_SENTRY_DSN = ""
        NEXT_PUBLIC_CRISP_WEBSITE_ID = ""
        LEMONSQUEEZY_API_KEY = ""
        LEMONSQUEEZY_WEBHOOK_SECRET = ""
        LEMONSQUEEZY_STORE_ID = ""
        LEMONSQUEEZY_DEFAULT_VARIANT_ID = ""
        LEMONSQUEEZY_TEST_VARIANT_ID = ""
    }
}

function Write-EnvFile {
    param(
        [string]$Path,
        [System.Collections.IDictionary]$Values
    )

    $lines = foreach ($key in $Values.Keys) {
        "$key=$($Values[$key])"
    }
    Set-Content -LiteralPath $Path -Value $lines -Encoding ASCII
}

function Invoke-Validator {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $Command 2>&1
        return [pscustomobject]@{
            Name = $Name
            ExitCode = $LASTEXITCODE
            Output = ($output | Out-String)
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Assert-ExitCode {
    param(
        [pscustomobject]$Result,
        [int]$Expected
    )

    if ($Result.ExitCode -ne $Expected) {
        throw "$($Result.Name) expected exit $Expected but got $($Result.ExitCode). Output:`n$($Result.Output)"
    }
}

function Test-Case {
    param(
        [string]$Name,
        [System.Collections.IDictionary]$Values,
        [int]$ExpectedExit,
        [string[]]$ExpectedOutputPatterns = @()
    )

    $envPath = Join-Path $tempDir "$Name.env"
    Write-EnvFile -Path $envPath -Values $Values

    $psResult = Invoke-Validator "PowerShell $Name" {
        & $script:PowerShellExe -NoProfile -File $validatorPs -ShifterDir $ShifterDir -EnvFile $envPath
    }
    Assert-ExitCode $psResult $ExpectedExit
    foreach ($pattern in $ExpectedOutputPatterns) {
        if ($psResult.Output -notmatch [regex]::Escape($pattern)) {
            throw "$($psResult.Name) output did not contain expected text '$pattern'. Output:`n$($psResult.Output)"
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($script:BashPath)) {
        $bashResult = Invoke-Validator "Bash $Name" {
            & $script:BashPath -lc "cd '$($ShifterDir -replace '\\', '/')' && ENV_FILE='$($envPath -replace '\\', '/')' bash infra/scripts/validate-customer-env.sh"
        }
        Assert-ExitCode $bashResult $ExpectedExit
        foreach ($pattern in $ExpectedOutputPatterns) {
            if ($bashResult.Output -notmatch [regex]::Escape($pattern)) {
                throw "$($bashResult.Name) output did not contain expected text '$pattern'. Output:`n$($bashResult.Output)"
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($BashPath)) {
    $bashCandidates = @(
        "C:\Program Files\Git\bin\bash.exe",
        "C:\Program Files\Git\usr\bin\bash.exe"
    )
    foreach ($candidate in $bashCandidates) {
        if (Test-Path -LiteralPath $candidate) {
            $BashPath = $candidate
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($BashPath)) {
        $bashCommand = Get-Command bash -ErrorAction SilentlyContinue
        if ($bashCommand -and $bashCommand.Source -notlike "*System32*") {
            $BashPath = $bashCommand.Source
        }
    }
}
$script:BashPath = $BashPath

$pwshCommand = Get-Command pwsh -ErrorAction SilentlyContinue
if ($pwshCommand) {
    $script:PowerShellExe = $pwshCommand.Source
}
else {
    $script:PowerShellExe = (Get-Command powershell -ErrorAction Stop).Source
}

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-env-validator-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    $valid = New-BaseEnv
    Test-Case -Name "valid-private-disabled-ai" -Values $valid -ExpectedExit 0

    $publicNoExport = New-BaseEnv
    $publicNoExport["AI_NO_EXPORT_REQUIRED"] = "true"
    $publicNoExport["AI_BASE_URL"] = "https://api.openai.com/v1"
    $publicNoExport["AI_MODEL"] = "gpt-4o"
    Test-Case -Name "reject-public-no-export-ai" -Values $publicNoExport -ExpectedExit 1

    $privateNoExport = New-BaseEnv
    $privateNoExport["AI_NO_EXPORT_REQUIRED"] = "true"
    $privateNoExport["AI_BASE_URL"] = "http://local-ai.customer.internal:8000/v1"
    $privateNoExport["AI_MODEL"] = "customer-approved-model"
    Test-Case -Name "allow-private-no-export-ai" -Values $privateNoExport -ExpectedExit 0

    $shortFieldKey = New-BaseEnv
    $shortFieldKey["FIELD_ENCRYPTION_KEY"] = "too-short"
    Test-Case -Name "reject-short-field-key" -Values $shortFieldKey -ExpectedExit 1

    $missingLicense = New-BaseEnv
    $missingLicense["SHIFTER_LICENSE_KEY"] = ""
    Test-Case `
        -Name "reject-missing-license-key" `
        -Values $missingLicense `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("SHIFTER_LICENSE_KEY is required")

    $shortLicense = New-BaseEnv
    $shortLicense["SHIFTER_LICENSE_KEY"] = "too-short"
    Test-Case `
        -Name "reject-short-license-key" `
        -Values $shortLicense `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("SHIFTER_LICENSE_KEY must be at least 24 characters")

    $partialResend = New-BaseEnv
    $partialResend["RESEND_API_KEY"] = "re_customer_key"
    Test-Case `
        -Name "reject-partial-resend" `
        -Values $partialResend `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("RESEND_FROM_EMAIL is required", "RESEND_FROM_NAME is required")

    $partialTwilio = New-BaseEnv
    $partialTwilio["TWILIO_ACCOUNT_SID"] = "twilio-account"
    Test-Case `
        -Name "reject-partial-twilio" `
        -Values $partialTwilio `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("Twilio is partially configured")

    $partialVapid = New-BaseEnv
    $partialVapid["VAPID_PUBLIC_KEY"] = "public-key"
    Test-Case `
        -Name "reject-partial-vapid" `
        -Values $partialVapid `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("Web Push VAPID is partially configured")

    $mismatchedVapid = New-BaseEnv
    $mismatchedVapid["VAPID_PUBLIC_KEY"] = "public-key"
    $mismatchedVapid["VAPID_PRIVATE_KEY"] = "private-key"
    $mismatchedVapid["VAPID_SUBJECT"] = "mailto:support@acme.test"
    $mismatchedVapid["NEXT_PUBLIC_VAPID_PUBLIC_KEY"] = "different-public-key"
    Test-Case `
        -Name "reject-mismatched-public-vapid" `
        -Values $mismatchedVapid `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("NEXT_PUBLIC_VAPID_PUBLIC_KEY must match VAPID_PUBLIC_KEY")

    $partialPushover = New-BaseEnv
    $partialPushover["PUSHOVER_USER_KEY"] = "pushover-user"
    Test-Case `
        -Name "reject-partial-pushover" `
        -Values $partialPushover `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("Pushover health alerts is partially configured")

    $partialLemonSqueezy = New-BaseEnv
    $partialLemonSqueezy["LEMONSQUEEZY_API_KEY"] = "ls_customer_key"
    Test-Case `
        -Name "reject-partial-lemonsqueezy" `
        -Values $partialLemonSqueezy `
        -ExpectedExit 1 `
        -ExpectedOutputPatterns @("LemonSqueezy is partially configured")

    $externalProcessors = New-BaseEnv
    $externalProcessors["NEXT_PUBLIC_POSTHOG_KEY"] = "phc_customer_approved"
    $externalProcessors["NEXT_PUBLIC_SENTRY_DSN"] = "https://public@sentry.example/1"
    $externalProcessors["NEXT_PUBLIC_CRISP_WEBSITE_ID"] = "customer-crisp-id"
    $externalProcessors["LEMONSQUEEZY_API_KEY"] = "ls_customer_approved"
    $externalProcessors["LEMONSQUEEZY_WEBHOOK_SECRET"] = "ls_webhook_secret"
    $externalProcessors["LEMONSQUEEZY_STORE_ID"] = "12345"
    $externalProcessors["LEMONSQUEEZY_DEFAULT_VARIANT_ID"] = "67890"
    $externalProcessors["LEMONSQUEEZY_TEST_VARIANT_ID"] = "67891"
    Test-Case `
        -Name "warn-external-processors" `
        -Values $externalProcessors `
        -ExpectedExit 0 `
        -ExpectedOutputPatterns @(
            "NEXT_PUBLIC_POSTHOG_KEY is set",
            "NEXT_PUBLIC_SENTRY_DSN is set",
            "NEXT_PUBLIC_CRISP_WEBSITE_ID is set",
            "LEMONSQUEEZY_API_KEY is set"
        )

    Write-Host "Customer env validator tests passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
