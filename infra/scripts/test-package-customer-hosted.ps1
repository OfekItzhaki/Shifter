param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $ShifterDir).Path
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

    Write-Host "Customer-hosted package assembly test passed." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $tempDir) {
        Remove-Item -LiteralPath $tempDir -Recurse -Force
    }
}
