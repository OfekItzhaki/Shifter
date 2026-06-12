param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = "",
    [switch]$SkipDockerComposeConfig
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
$customerEnv = Join-Path $root "infra\compose\.env.customer.example"

Invoke-Step "PowerShell customer env validator harness" {
    & (Join-Path $PSScriptRoot "test-customer-env-validator.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Restore dry-run harness" {
    & (Join-Path $PSScriptRoot "test-restore-compose-dry-run.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Backup compose harness" {
    & (Join-Path $PSScriptRoot "test-backup-compose.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Deploy compose happy-path harness" {
    & (Join-Path $PSScriptRoot "test-deploy-compose.ps1") -ShifterDir $root -BashPath $bash
}

Invoke-Step "Deploy compose rollback harness" {
    & (Join-Path $PSScriptRoot "test-deploy-compose-rollback.ps1") -BashPath $bash
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

        & docker compose --env-file $customerEnv -f $composeFile config --quiet
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose config failed with exit code $LASTEXITCODE."
        }
    }
}

Write-Host "Customer-hosted package preflight passed." -ForegroundColor Green
