param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$OutputDir = "",
    [string]$PackageName = "",
    [switch]$DryRun,
    [switch]$ListFiles,
    [switch]$NoArchive
)

$ErrorActionPreference = "Stop"

function Normalize-RepoPath {
    param([string]$Path)
    return ($Path -replace '\\', '/').TrimStart('/')
}

function Test-IncludedPath {
    param([string]$Path)

    $normalized = Normalize-RepoPath $Path

    foreach ($exact in $script:ExactIncludes) {
        if ($normalized -eq $exact) {
            return $true
        }
    }

    foreach ($prefix in $script:PrefixIncludes) {
        if ($normalized.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Test-ExcludedPath {
    param([string]$Path)

    $normalized = Normalize-RepoPath $Path
    $fileName = Split-Path $normalized -Leaf

    if ($normalized -match '(^|/)(node_modules|\.next|dist|bin|obj|\.git|artifacts|logs|playwright-report|test-results)(/|$)') {
        return $true
    }

    if ($normalized -match '(^|/)\.env($|\.|/)' -and $normalized -ne 'infra/compose/.env.customer.example') {
        return $true
    }

    if ($normalized -match '(private|secret).*\.(pem|key|json)$') {
        return $true
    }

    if ($normalized -match 'license.*\.(json|pem|key)$' -and $normalized -ne 'infra/compose/license.customer.example.json') {
        return $true
    }

    if ($fileName -match '\.(log|tmp|bak)$') {
        return $true
    }

    return $false
}

function Copy-PackageFile {
    param(
        [string]$Root,
        [string]$PackageRoot,
        [string]$RelativePath
    )

    $source = Join-Path $Root ($RelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $target = Join-Path $PackageRoot ($RelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    $targetDir = Split-Path $target -Parent

    if (-not (Test-Path -LiteralPath $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir | Out-Null
    }

    Copy-Item -LiteralPath $source -Destination $target
}

function New-PackageArchive {
    param(
        [string]$PackageRoot,
        [string]$PackageName,
        [string]$ArchivePath
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::Open($ArchivePath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $rootWithSeparator = $PackageRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        foreach ($file in Get-ChildItem -LiteralPath $PackageRoot -Recurse -File -Force) {
            $relativePath = $file.FullName.Substring($rootWithSeparator.Length)
            $entryName = "$PackageName/$($relativePath -replace '\\', '/')"
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

$root = (Resolve-Path $ShifterDir).Path

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "artifacts\customer-hosted\packages"
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
if ([string]::IsNullOrWhiteSpace($PackageName)) {
    $PackageName = "shifter-customer-hosted-$timestamp"
}

$script:PrefixIncludes = @(
    "apps/api/",
    "apps/web/",
    "apps/solver/",
    "infra/docker/",
    "infra/migrations/"
)

$script:ExactIncludes = @(
    "README.md",
    "docs/AI-DEPLOYMENT-MODES.md",
    "docs/CLOUDFLARE-EDGE-SECURITY.md",
    "docs/CUSTOMER-HOSTED-DEPLOYMENT.md",
    "docs/MANUAL-SELF-SERVICE-QA-CHECKLIST.md",
    "docs/MANUAL-SELF-SERVICE-SCHEDULING.md",
    "docs/SELF-SERVICE-HOLIDAY-CALENDAR-CONTRACT.md",
    "docs/SELF-SERVICE-PORTABILITY-CONTRACT.md",
    "infra/compose/.env.customer.example",
    "infra/compose/docker-compose.yml",
    "infra/compose/license.customer.example.json",
    "infra/scripts/backup-compose.sh",
    "infra/scripts/bundle-compose-images.sh",
    "infra/scripts/deploy-compose.sh",
    "infra/scripts/generate-signed-license.ps1",
    "infra/scripts/package-customer-hosted.ps1",
    "infra/scripts/restore-compose.sh",
    "infra/scripts/seed-compose.sh",
    "infra/scripts/seed.sql",
    "infra/scripts/smoke-organization-import-postgres.ps1",
    "infra/scripts/smoke-self-service-client-ready.ps1",
    "infra/scripts/test-backup-compose.ps1",
    "infra/scripts/test-bundle-compose-images.ps1",
    "infra/scripts/test-customer-env-validator.ps1",
    "infra/scripts/test-customer-hosted-package.ps1",
    "infra/scripts/test-deploy-compose-rollback.ps1",
    "infra/scripts/test-deploy-compose.ps1",
    "infra/scripts/test-generate-signed-license.ps1",
    "infra/scripts/test-package-customer-hosted.ps1",
    "infra/scripts/test-restore-compose-dry-run.ps1",
    "infra/scripts/test-seed-compose-dry-run.ps1",
    "infra/scripts/test-verify-customer-hosted-install-dry-run.ps1",
    "infra/scripts/validate-customer-env.ps1",
    "infra/scripts/validate-customer-env.sh",
    "infra/scripts/verify-customer-hosted-install.ps1"
)

$requiredPaths = @(
    "apps/api/Jobuler.sln",
    "apps/web/package.json",
    "apps/solver/requirements.txt",
    "infra/compose/docker-compose.yml",
    "infra/compose/.env.customer.example",
    "infra/compose/license.customer.example.json",
    "infra/docker/api.Dockerfile",
    "infra/docker/web.Dockerfile",
    "infra/docker/solver.Dockerfile",
    "infra/scripts/validate-customer-env.ps1",
    "infra/scripts/validate-customer-env.sh",
    "infra/scripts/verify-customer-hosted-install.ps1",
    "infra/scripts/test-customer-hosted-package.ps1",
    "infra/scripts/smoke-self-service-client-ready.ps1",
    "infra/scripts/generate-signed-license.ps1",
    "docs/CUSTOMER-HOSTED-DEPLOYMENT.md",
    "docs/AI-DEPLOYMENT-MODES.md",
    "docs/MANUAL-SELF-SERVICE-SCHEDULING.md"
)

foreach ($requiredPath in $requiredPaths) {
    $absoluteRequiredPath = Join-Path $root ($requiredPath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $absoluteRequiredPath)) {
        throw "Required customer-hosted package file is missing: $requiredPath"
    }
}

$git = Get-Command git -ErrorAction SilentlyContinue
if (-not $git) {
    throw "git was not found. The customer-hosted package is assembled from tracked files."
}

$trackedFiles = & git -C $root ls-files
if ($LASTEXITCODE -ne 0) {
    throw "git ls-files failed with exit code $LASTEXITCODE."
}

$packageFiles = $trackedFiles |
    ForEach-Object { Normalize-RepoPath $_ } |
    Where-Object { Test-IncludedPath $_ } |
    Where-Object { -not (Test-ExcludedPath $_) } |
    Sort-Object -Unique

foreach ($requiredPath in $requiredPaths) {
    if ($packageFiles -notcontains $requiredPath) {
        throw "Required customer-hosted package file was not selected: $requiredPath"
    }
}

$unsafeSelected = $packageFiles | Where-Object { Test-ExcludedPath $_ }
if ($unsafeSelected.Count -gt 0) {
    throw "Unsafe files were selected for the customer-hosted package:`n$($unsafeSelected -join "`n")"
}

Write-Host "Customer-hosted package plan:"
Write-Host "  Root: $root"
Write-Host "  Name: $PackageName"
Write-Host "  Files: $($packageFiles.Count)"

if ($DryRun) {
    if ($ListFiles) {
        $packageFiles | ForEach-Object { Write-Host "  $_" }
    }
    Write-Host "Customer-hosted package dry run passed." -ForegroundColor Green
    return
}

$outputRoot = (New-Item -ItemType Directory -Path $OutputDir -Force).FullName
$stagingRoot = Join-Path $outputRoot $PackageName

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingRoot | Out-Null

foreach ($packageFile in $packageFiles) {
    Copy-PackageFile -Root $root -PackageRoot $stagingRoot -RelativePath $packageFile
}

$manifestPath = Join-Path $stagingRoot "CUSTOMER-HOSTED-MANIFEST.txt"
$manifest = @(
    "Shifter customer-hosted package",
    "Generated: $(Get-Date -Format o)",
    "Source: $(git -C $root rev-parse --short HEAD)",
    "File count: $($packageFiles.Count)",
    "",
    "Files:"
) + $packageFiles
Set-Content -LiteralPath $manifestPath -Encoding ASCII -Value $manifest

if (-not $NoArchive) {
    $archivePath = Join-Path $outputRoot "$PackageName.zip"
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    New-PackageArchive -PackageRoot $stagingRoot -PackageName $PackageName -ArchivePath $archivePath
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $shaPath = "$archivePath.sha256"
    Set-Content -LiteralPath $shaPath -Encoding ASCII -Value "$archiveHash  $PackageName.zip"
    Write-Host "Archive: $archivePath"
    Write-Host "SHA-256: $shaPath"
}

Write-Host "Package directory: $stagingRoot"
Write-Host "Customer-hosted package created." -ForegroundColor Green
