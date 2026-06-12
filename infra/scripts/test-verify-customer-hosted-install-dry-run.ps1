param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = ""
)

$ErrorActionPreference = "Stop"

$wrapperScript = Join-Path $PSScriptRoot "verify-customer-hosted-install.ps1"
$exampleEnv = Join-Path $ShifterDir "infra\compose\.env.customer.example"

$command = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $wrapperScript,
    "-ShifterDir", $ShifterDir,
    "-EnvFile", $exampleEnv,
    "-SkipPackagePreflight",
    "-SeedDryRun",
    "-ResolveOnly"
)

if (-not [string]::IsNullOrWhiteSpace($BashPath)) {
    $command += @("-BashPath", $BashPath)
}

$output = & powershell @command 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "verify-customer-hosted-install dry run failed with exit code $LASTEXITCODE. Output:`n$($output | Out-String)"
}

$text = $output | Out-String
foreach ($pattern in @(
        "Seed dry run passed",
        "Resolved smoke configuration:",
        "WebBaseUrl: https://shifter.customer.example",
        "ApiBaseUrl: https://api-shifter.customer.example",
        "Customer-hosted install verification completed"
    )) {
    if ($text -notmatch [regex]::Escape($pattern)) {
        throw "verify-customer-hosted-install dry run output is missing '$pattern'. Output:`n$text"
    }
}

Write-Host "Customer-hosted install wrapper dry-run test passed." -ForegroundColor Green
