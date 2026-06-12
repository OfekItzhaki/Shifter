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

function Set-EnvLine {
    param(
        [string]$Text,
        [string]$Key,
        [string]$Value
    )

    $pattern = "(?m)^$([regex]::Escape($Key))=.*$"
    $replacement = "$Key=$Value"
    if ($Text -notmatch $pattern) {
        return "$Text`n$replacement"
    }

    return [regex]::Replace($Text, $pattern, $replacement)
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
    $composeProbeEnvFile = Join-Path $extractDir "self-service-defaults-probe.env"

    if (-not $SkipDockerComposeConfig) {
        $docker = Get-Command docker -ErrorAction SilentlyContinue
        if (-not $docker) {
            throw "Docker was not found. Re-run with -SkipDockerComposeConfig to skip extracted package compose validation."
        }

        $probeEnvText = Get-Content -LiteralPath $packagedEnvFile -Raw
        $selfServiceDefaultsProbe = [ordered]@{
            SELF_SERVICE_DEFAULT_MIN_SHIFTS_PER_CYCLE = "2"
            SELF_SERVICE_DEFAULT_MAX_SHIFTS_PER_CYCLE = "4"
            SELF_SERVICE_DEFAULT_REQUEST_WINDOW_OPEN_OFFSET_HOURS = "96"
            SELF_SERVICE_DEFAULT_REQUEST_WINDOW_CLOSE_OFFSET_HOURS = "12"
            SELF_SERVICE_DEFAULT_CANCELLATION_CUTOFF_HOURS = "36"
            SELF_SERVICE_DEFAULT_MAX_ABSENCES_PER_CYCLE = "2"
            SELF_SERVICE_DEFAULT_MAX_LATE_CANCELLATIONS_PER_CYCLE = "1"
            SELF_SERVICE_DEFAULT_LATE_CANCELLATION_WINDOW_HOURS = "18"
            SELF_SERVICE_DEFAULT_WAITLIST_OFFER_MINUTES = "45"
            SELF_SERVICE_DEFAULT_CYCLE_DURATION_DAYS = "14"
            SELF_SERVICE_DEFAULT_ALLOW_MEMBER_SHIFT_CLAIMS = "false"
            SELF_SERVICE_DEFAULT_ALLOW_WAITLIST = "false"
            SELF_SERVICE_DEFAULT_ALLOW_SHIFT_CHANGE_REQUESTS = "false"
            SELF_SERVICE_DEFAULT_ALLOW_ABSENCE_REPORTS = "false"
            SELF_SERVICE_DEFAULT_ALLOW_SHIFT_SWAPS = "false"
        }

        foreach ($entry in $selfServiceDefaultsProbe.GetEnumerator()) {
            $probeEnvText = Set-EnvLine $probeEnvText $entry.Key $entry.Value
        }

        Set-Content -LiteralPath $composeProbeEnvFile -Value $probeEnvText

        $composeOutput = & docker compose --env-file $composeProbeEnvFile -f $packagedComposeFile config 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Extracted package docker compose config failed with exit code $LASTEXITCODE. Output:`n$($composeOutput | Out-String)"
        }

        $composeText = $composeOutput | Out-String
        $expectedApiEnvironment = [ordered]@{
            "SelfServiceDefaults__MinShiftsPerCycle" = "2"
            "SelfServiceDefaults__MaxShiftsPerCycle" = "4"
            "SelfServiceDefaults__RequestWindowOpenOffsetHours" = "96"
            "SelfServiceDefaults__RequestWindowCloseOffsetHours" = "12"
            "SelfServiceDefaults__CancellationCutoffHours" = "36"
            "SelfServiceDefaults__MaxAbsencesPerCycle" = "2"
            "SelfServiceDefaults__MaxLateCancellationsPerCycle" = "1"
            "SelfServiceDefaults__LateCancellationWindowHours" = "18"
            "SelfServiceDefaults__WaitlistOfferMinutes" = "45"
            "SelfServiceDefaults__CycleDurationDays" = "14"
            "SelfServiceDefaults__AllowMemberShiftClaims" = "false"
            "SelfServiceDefaults__AllowWaitlist" = "false"
            "SelfServiceDefaults__AllowShiftChangeRequests" = "false"
            "SelfServiceDefaults__AllowAbsenceReports" = "false"
            "SelfServiceDefaults__AllowShiftSwaps" = "false"
        }

        foreach ($entry in $expectedApiEnvironment.GetEnumerator()) {
            Assert-TextContains `
                $composeText `
                "$($entry.Key): `"$($entry.Value)`"" `
                "Extracted package docker compose config"
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
