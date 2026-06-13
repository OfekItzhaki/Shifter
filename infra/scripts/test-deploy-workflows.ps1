param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Needle,
        [string]$Message
    )

    if (-not $Text.Contains($Needle)) {
        throw $Message
    }
}

function Assert-Before {
    param(
        [string]$Text,
        [string]$Before,
        [string]$After,
        [string]$Message
    )

    $beforeIndex = $Text.IndexOf($Before, [StringComparison]::Ordinal)
    $afterIndex = $Text.IndexOf($After, [StringComparison]::Ordinal)

    if ($beforeIndex -lt 0 -or $afterIndex -lt 0 -or $beforeIndex -ge $afterIndex) {
        throw $Message
    }
}

$root = (Resolve-Path $ShifterDir).Path
$productionWorkflow = Get-Content -LiteralPath (Join-Path $root ".github\workflows\deploy-vps.yml") -Raw
$stagingWorkflow = Get-Content -LiteralPath (Join-Path $root ".github\workflows\deploy-staging.yml") -Raw

Assert-Contains $productionWorkflow "github.ref_name != 'main'" "Production deploy workflow must fail fast outside main."
Assert-Contains $productionWorkflow 'test "$(git rev-parse HEAD)" = "${{ github.sha }}"' "Production deploy workflow must verify the checked-out SHA."
Assert-Contains $productionWorkflow "infra/scripts/backup-compose.sh" "Production deploy workflow must run a pre-deploy backup."
Assert-Before $productionWorkflow "infra/scripts/backup-compose.sh" "infra/scripts/run-migrations.sh" "Production backup must run before migrations."
Assert-Contains $productionWorkflow "http://localhost:5000/ready" "Production deploy workflow must check API readiness."
Assert-Contains $productionWorkflow "smoke-hosted-vps.ps1" "Production deploy workflow must run hosted smoke after deploy."
Assert-Contains $productionWorkflow "command_timeout: 15m" "Production deploy SSH timeout must allow backup and deploy work."

Assert-Contains $stagingWorkflow "environment: staging" "Staging workflow must use the GitHub staging environment."
Assert-Contains $stagingWorkflow "github.ref_name != 'develop'" "Staging deploy workflow must fail fast outside develop."
Assert-Contains $stagingWorkflow 'EXPECTED_REVISION="${{ github.sha }}"' "Staging workflow must pass the intended GitHub SHA to deploy-compose."
Assert-Contains $stagingWorkflow "smoke-hosted-vps.ps1" "Staging workflow must run hosted smoke when URLs are configured."
Assert-Contains $stagingWorkflow "STAGING_BASIC_AUTH_USERNAME" "Staging workflow must support Basic Auth for protected staging smoke."
Assert-Contains $stagingWorkflow "BasicAuthUsername" "Staging workflow must pass Basic Auth credentials to hosted smoke when configured."

Write-Host "Deploy workflow test passed." -ForegroundColor Green
