param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = ""
)

$ErrorActionPreference = "Stop"

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

$root = (Resolve-Path $ShifterDir).Path
$bash = Find-Bash $BashPath
$packageScript = Join-Path $PSScriptRoot "package-customer-hosted.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-package-test-$([Guid]::NewGuid().ToString('N'))"
$packageName = "shifter-customer-hosted-test"

try {
    $dryRunOutput = & $packageScript -ShifterDir $root -DryRun 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Customer-hosted package dry run failed with exit code $LASTEXITCODE. Output:`n$($dryRunOutput | Out-String)"
    }

    & $packageScript -ShifterDir $root -OutputDir $tempDir -PackageName $packageName 2>&1 | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        throw "Customer-hosted package creation failed with exit code $LASTEXITCODE."
    }

    $packageRoot = Join-Path $tempDir $packageName
    $archivePath = Join-Path $tempDir "$packageName.zip"
    $manifestPath = Join-Path $packageRoot "CUSTOMER-HOSTED-MANIFEST.txt"

    foreach ($expectedPath in @($packageRoot, $archivePath, $manifestPath)) {
        if (-not (Test-Path -LiteralPath $expectedPath)) {
            throw "Expected package artifact was not created: $expectedPath"
        }
    }

    foreach ($relativePath in @(
            "infra\compose\docker-compose.yml",
            "infra\compose\.env.customer.example",
            "infra\scripts\validate-customer-env.ps1",
            "infra\scripts\verify-customer-hosted-install.ps1",
            "infra\scripts\test-customer-hosted-package.ps1",
            "infra\scripts\smoke-self-service-client-ready.ps1",
            "infra\scripts\smoke-organization-import-postgres.ps1",
            "infra\scripts\test-package-customer-hosted.ps1",
            "docs\AI-DEPLOYMENT-MODES.md",
            "apps\web\package.json"
        )) {
        $expectedPath = Join-Path $packageRoot $relativePath
        if (-not (Test-Path -LiteralPath $expectedPath)) {
            throw "Package is missing expected file: $relativePath"
        }
    }

    foreach ($blockedPattern in @(
            "\.env$",
            "private.*\.(pem|key|json)$",
            "secret.*\.(pem|key|json)$",
            "node_modules",
            "\\\.git(\\|$)"
        )) {
        $blocked = Get-ChildItem -LiteralPath $packageRoot -Recurse -Force |
            Where-Object { $_.FullName -match $blockedPattern -and $_.Name -ne ".env.customer.example" }
        if ($blocked) {
            throw "Package contains blocked path matching $blockedPattern`: $($blocked.FullName -join ', ')"
        }
    }

    $packagedVerifier = Join-Path $packageRoot "infra\scripts\verify-customer-hosted-install.ps1"
    $packagedEnvFile = Join-Path $packageRoot "infra\compose\.env.customer.example"
    $verifierOutput = & $packagedVerifier `
        -ShifterDir $packageRoot `
        -EnvFile $packagedEnvFile `
        -BashPath $bash `
        -SkipPackagePreflight `
        -SkipLiveSmoke `
        -SeedDryRun `
        -ResolveOnly 2>&1
    $verifierOutput | ForEach-Object { Write-Host $_ }
    if ($LASTEXITCODE -ne 0) {
        throw "Packaged install verifier dry run failed with exit code $LASTEXITCODE. Output:`n$($verifierOutput | Out-String)"
    }

    Write-Host "Customer-hosted package assembly test passed." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $tempDir) {
        Remove-Item -LiteralPath $tempDir -Recurse -Force
    }
}
