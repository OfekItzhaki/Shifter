param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = "",
    [switch]$SkipDockerComposeConfig
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

function Join-PackagePath {
    param(
        [string]$Root,
        [string]$RelativePath
    )

    $parts = $RelativePath -split '[\\/]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $resolved = $Root
    foreach ($part in $parts) {
        $resolved = Join-Path $resolved $part
    }

    return $resolved
}

function Assert-TextContains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Context
    )

    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) {
        throw "$Context is missing expected text '$Expected'."
    }
}

$root = (Resolve-Path $ShifterDir).Path
$bash = Find-Bash $BashPath
$packageScript = Join-Path $PSScriptRoot "package-customer-hosted.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-package-test-$([Guid]::NewGuid().ToString('N'))"
$extractDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-package-extract-test-$([Guid]::NewGuid().ToString('N'))"
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
    $shaPath = "$archivePath.sha256"
    $manifestPath = Join-Path $packageRoot "CUSTOMER-HOSTED-MANIFEST.txt"

    foreach ($expectedPath in @($packageRoot, $archivePath, $shaPath, $manifestPath)) {
        if (-not (Test-Path -LiteralPath $expectedPath)) {
            throw "Expected package artifact was not created: $expectedPath"
        }
    }

    $expectedHashLine = Get-Content -LiteralPath $shaPath -Raw
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expectedHashLine.Trim() -ne "$actualHash  $packageName.zip") {
        throw "Package SHA-256 sidecar did not match archive hash. Sidecar: $expectedHashLine Actual: $actualHash"
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractDir
    $extractedPackageRoot = Join-Path $extractDir $packageName
    $extractedManifestPath = Join-Path $extractedPackageRoot "CUSTOMER-HOSTED-MANIFEST.txt"

    foreach ($expectedPath in @($extractedPackageRoot, $extractedManifestPath)) {
        if (-not (Test-Path -LiteralPath $expectedPath)) {
            throw "Expected extracted package artifact was not found: $expectedPath"
        }
    }

    foreach ($relativePath in @(
            "infra/compose/docker-compose.yml",
            "infra/compose/.env.customer.example",
            "infra/scripts/validate-customer-env.ps1",
            "infra/scripts/verify-customer-hosted-install.ps1",
            "infra/scripts/test-customer-hosted-package.ps1",
            "infra/scripts/smoke-self-service-client-ready.ps1",
            "infra/scripts/smoke-organization-import-postgres.ps1",
            "infra/scripts/test-package-customer-hosted.ps1",
            "docs/AI-DEPLOYMENT-MODES.md",
            "docs/CUSTOMER-HOSTED-HANDOFF-NOTES.md",
            "apps/web/package.json"
        )) {
        $expectedPath = Join-PackagePath -Root $extractedPackageRoot -RelativePath $relativePath
        if (-not (Test-Path -LiteralPath $expectedPath)) {
            throw "Package is missing expected file: $relativePath"
        }
    }

    foreach ($blockedPattern in @(
            "\.env$",
            "private.*\.(pem|key|json)$",
            "secret.*\.(pem|key|json)$",
            "node_modules",
            "[\\/]\.git([\\/]|$)"
        )) {
        $blocked = Get-ChildItem -LiteralPath $extractedPackageRoot -Recurse -Force |
            Where-Object { $_.FullName -match $blockedPattern -and $_.Name -ne ".env.customer.example" }
        if ($blocked) {
            throw "Package contains blocked path matching $blockedPattern`: $($blocked.FullName -join ', ')"
        }
    }

    $packagedEnvFile = Join-PackagePath -Root $extractedPackageRoot -RelativePath "infra/compose/.env.customer.example"
    $packagedComposeFile = Join-PackagePath -Root $extractedPackageRoot -RelativePath "infra/compose/docker-compose.yml"

    if (-not $SkipDockerComposeConfig) {
        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if (-not $docker) {
            throw "Docker was not found. Re-run with -SkipDockerComposeConfig to skip extracted package compose validation."
        }

        $composeOutput = & docker compose --env-file $packagedEnvFile -f $packagedComposeFile config 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Extracted package docker compose config failed with exit code $LASTEXITCODE. Output:`n$($composeOutput | Out-String)"
        }

        $composeText = $composeOutput | Out-String
        foreach ($expected in @(
                "SelfServiceDefaults__MinShiftsPerCycle",
                "SelfServiceDefaults__MaxShiftsPerCycle",
                "SelfServiceDefaults__RequestWindowOpenOffsetHours",
                "SelfServiceDefaults__RequestWindowCloseOffsetHours",
                "SelfServiceDefaults__CancellationCutoffHours",
                "SelfServiceDefaults__MaxAbsencesPerCycle",
                "SelfServiceDefaults__MaxLateCancellationsPerCycle",
                "SelfServiceDefaults__LateCancellationWindowHours",
                "SelfServiceDefaults__WaitlistOfferMinutes",
                "SelfServiceDefaults__CycleDurationDays",
                "SelfServiceDefaults__AllowMemberShiftClaims",
                "SelfServiceDefaults__AllowWaitlist",
                "SelfServiceDefaults__AllowShiftChangeRequests",
                "SelfServiceDefaults__AllowAbsenceReports",
                "SelfServiceDefaults__AllowShiftSwaps"
            )) {
            Assert-TextContains $composeText $expected "Extracted package docker compose config"
        }
    }

    $packagedVerifier = Join-PackagePath -Root $extractedPackageRoot -RelativePath "infra/scripts/verify-customer-hosted-install.ps1"
    $verifierOutput = & $packagedVerifier `
        -ShifterDir $extractedPackageRoot `
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
    if (Test-Path -LiteralPath $extractDir) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force
    }
}
