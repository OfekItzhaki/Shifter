param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$EnvFile = "",
    [string]$BashPath = "",
    [switch]$SkipPackagePreflight,
    [switch]$SkipDockerComposeConfig,
    [switch]$SkipPostgresImportSmoke,
    [switch]$SkipSeed,
    [switch]$SeedDryRun,
    [switch]$SkipLiveSmoke,
    [switch]$ResolveOnly,
    [switch]$SkipBrowserTest
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
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
if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    $EnvFile = Join-Path $root "infra\compose\.env"
}
$resolvedEnvFile = (Resolve-Path $EnvFile).Path
$bash = Find-Bash $BashPath

if (-not $SkipPackagePreflight) {
    Invoke-Step "Customer-hosted package preflight" {
        $preflightArgs = @{
            ShifterDir = $root
            BashPath = $bash
            EnvFile = $resolvedEnvFile
            ValidateEnvFile = $true
        }

        if ($SkipDockerComposeConfig) {
            $preflightArgs.SkipDockerComposeConfig = $true
        }
        if ($SkipPostgresImportSmoke) {
            $preflightArgs.SkipPostgresImportSmoke = $true
        }

        & (Join-Path $PSScriptRoot "test-customer-hosted-package.ps1") @preflightArgs
    }
}

if (-not $SkipSeed) {
    Invoke-Step "Seeded demo data target check" {
        $dryRunValue = if ($SeedDryRun -or $ResolveOnly) { "1" } else { "0" }
        $command = @(
            "cd '$(To-BashPath $root)'",
            "&&",
            "DRY_RUN=$dryRunValue",
            "SHIFTER_DIR='$(To-BashPath $root)'",
            "ENV_FILE='$(To-BashPath $resolvedEnvFile)'",
            "bash '$(To-BashPath (Join-Path $PSScriptRoot "seed-compose.sh"))'"
        ) -join " "

        $output = & $bash -lc $command 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "seed-compose step failed with exit code $LASTEXITCODE. Output:`n$($output | Out-String)"
        }

        Write-Host ($output | Out-String)
    }
}

if (-not $SkipLiveSmoke) {
    Invoke-Step "Live self-service smoke" {
        $smokeArgs = @{
            EnvFile = $resolvedEnvFile
        }
        if ($SkipBrowserTest) {
            $smokeArgs.SkipBrowserTest = $true
        }
        if ($ResolveOnly) {
            $smokeArgs.ResolveOnly = $true
        }

        & (Join-Path $PSScriptRoot "smoke-self-service-client-ready.ps1") @smokeArgs
    }
}

Write-Host "Customer-hosted install verification completed." -ForegroundColor Green
