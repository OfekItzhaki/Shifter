param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = "",
    [string]$EnvFile = "",
    [switch]$ValidateEnvFile,
    [switch]$SkipDockerComposeConfig,
    [switch]$SkipPostgresImportSmoke
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "==> $Name"
    & $Action
    Write-Host "OK: $Name" -ForegroundColor Green
}

function Find-Bash {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return $RequestedPath
    }

    foreach ($candidate in @(
        "C:\Program Files\Git\bin\bash.exe",
        "C:\Program Files\Git\usr\bin\bash.exe"
    )) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $bashCommand = Get-Command bash -ErrorAction SilentlyContinue
    if ($bashCommand -and $bashCommand.Source -notlike "*System32*") {
        return $bashCommand.Source
    }

    throw "Git Bash was not found. Install Git for Windows or pass -BashPath."
}

function To-BashPath {
    param([string]$Path)
    $normalized = $Path -replace '\\', '/'
    if ($normalized -match '^([A-Za-z]):/(.*)$') {
        return "/$($Matches[1].ToLowerInvariant())/$($Matches[2])"
    }
    return $normalized
}

$root = (Resolve-Path $ShifterDir).Path
$bash = Find-Bash $BashPath
$composeFile = Join-Path $root "infra\compose\docker-compose.yml"
if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $root "infra\compose\.env.customer.example"
}
$composeEnv = (Resolve-Path $EnvFile).Path

Invoke-Step "PowerShell customer env validator harness" {
    & (Join-Path $PSScriptRoot "test-customer-env-validator.ps1") -ShifterDir $root -BashPath $bash
}

if ($ValidateEnvFile) {
    Invoke-Step "Customer env file validation" {
        & (Join-Path $PSScriptRoot "validate-customer-env.ps1") -ShifterDir $root -EnvFile $composeEnv
    }
}

Invoke-Step "Restore dry-run harness" {
    & (Join-Path $PSScriptRoot "test-restore-compose-dry-run.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Seed compose dry-run harness" {
    & (Join-Path $PSScriptRoot "test-seed-compose-dry-run.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Signed license generator harness" {
    & (Join-Path $PSScriptRoot "test-generate-signed-license.ps1") -ShifterDir $root
}

Invoke-Step "Offline image bundle harness" {
    & (Join-Path $PSScriptRoot "test-bundle-compose-images.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Customer-hosted install wrapper dry-run harness" {
    & (Join-Path $PSScriptRoot "test-verify-customer-hosted-install-dry-run.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Hosted VPS smoke harness" {
    & (Join-Path $PSScriptRoot "test-smoke-hosted-vps.ps1") -ShifterDir $root
}

Invoke-Step "Deploy workflow harness" {
    & (Join-Path $PSScriptRoot "test-deploy-workflows.ps1") -ShifterDir $root
}

Invoke-Step "Release readiness audit harness" {
    & (Join-Path $PSScriptRoot "test-check-release-readiness.ps1") -ShifterDir $root
}

Invoke-Step "GitHub staging setup harness" {
    & (Join-Path $PSScriptRoot "test-setup-github-staging.ps1") -ShifterDir $root
}

Invoke-Step "Staging smoke evidence harness" {
    & (Join-Path $PSScriptRoot "test-check-staging-smoke-evidence.ps1") -ShifterDir $root
}

Invoke-Step "Customer-hosted package assembly harness" {
    $packageArgs = @{
        ShifterDir = $root
        BashPath = $bash
    }
    if ($SkipDockerComposeConfig) {
        $packageArgs.SkipDockerComposeConfig = $true
    }

    & (Join-Path $PSScriptRoot "test-package-customer-hosted.ps1") @packageArgs
}

Invoke-Step "Backup compose harness" {
    & (Join-Path $PSScriptRoot "test-backup-compose.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Deploy compose happy-path harness" {
    & (Join-Path $PSScriptRoot "test-deploy-compose.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Deploy compose expected revision harness" {
    & (Join-Path $PSScriptRoot "test-deploy-compose-expected-revision.ps1") -BashPath $bash
}

Invoke-Step "Deploy compose rollback harness" {
    & (Join-Path $PSScriptRoot "test-deploy-compose-rollback.ps1") -BashPath $bash
}

Invoke-Step "PowerShell script syntax" {
    foreach ($scriptName in @(
            "verify-customer-hosted-install.ps1",
            "test-verify-customer-hosted-install-dry-run.ps1",
            "smoke-hosted-vps.ps1",
            "test-smoke-hosted-vps.ps1",
            "check-release-readiness.ps1",
            "test-check-release-readiness.ps1",
            "setup-github-staging.ps1",
            "test-setup-github-staging.ps1",
            "check-staging-smoke-evidence.ps1",
            "test-check-staging-smoke-evidence.ps1",
            "test-deploy-workflows.ps1",
            "test-bundle-compose-images.ps1",
            "generate-signed-license.ps1",
            "test-generate-signed-license.ps1",
            "package-customer-hosted.ps1",
            "test-package-customer-hosted.ps1",
            "test-deploy-compose-expected-revision.ps1"
        )) {
        $parseErrors = $null
        $tokens = $null
        [System.Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot $scriptName),
            [ref]$tokens,
            [ref]$parseErrors) | Out-Null

        if ($parseErrors.Count -gt 0) {
            throw "$scriptName has parse errors: $($parseErrors | Out-String)"
        }
    }
}

Invoke-Step "Compose script syntax" {
    $syntaxCommand = @(
        "cd '$(To-BashPath $root)'",
        "&&",
        "bash -n infra/scripts/backup-compose.sh",
        "&&",
        "bash -n infra/scripts/deploy-compose.sh",
        "&&",
        "bash -n infra/scripts/restore-compose.sh",
        "&&",
        "bash -n infra/scripts/seed-compose.sh",
        "&&",
        "bash -n infra/scripts/bundle-compose-images.sh",
        "&&",
        "bash -n infra/scripts/validate-customer-env.sh"
    ) -join " "

    $output = & $bash -lc $syntaxCommand 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Bash syntax checks failed with exit code $LASTEXITCODE. Output:`n$($output | Out-String)"
    }
}

if (-not $SkipDockerComposeConfig) {
    Invoke-Step "Customer Docker Compose config" {
        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if (-not $docker) {
            throw "Docker was not found. Re-run with -SkipDockerComposeConfig to skip this optional local check."
        }

        & docker compose --env-file $composeEnv -f $composeFile config --quiet
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose config failed with exit code $LASTEXITCODE."
        }
    }
}

if (-not $SkipPostgresImportSmoke) {
    Invoke-Step "PostgreSQL organization package import smoke" {
        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if (-not $docker) {
            throw "Docker was not found. Re-run with -SkipPostgresImportSmoke to skip the temporary PostgreSQL import smoke."
        }

        $smokeOutput = @()
        $smokeExitCode = 0
        try {
            $smokeOutput = & (Join-Path $PSScriptRoot "smoke-organization-import-postgres.ps1") 2>&1
            $smokeExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
        }
        catch {
            $smokeOutput += $_ | Out-String
            $smokeExitCode = 1
        }

        $smokeOutput | ForEach-Object { Write-Host $_ }
        if ($smokeExitCode -ne 0) {
            throw "PostgreSQL organization package import smoke failed with exit code $smokeExitCode. Output:`n$($smokeOutput | Out-String)"
        }
    }
}

Write-Host "Customer-hosted package preflight passed." -ForegroundColor Green
