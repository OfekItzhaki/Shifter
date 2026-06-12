param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = ""
)

$ErrorActionPreference = "Stop"

$seedScript = Join-Path $PSScriptRoot "seed-compose.sh"
$exampleEnv = Join-Path $ShifterDir "infra\compose\.env.customer.example"
$seedFile = Join-Path $ShifterDir "infra\scripts\seed.sql"

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

$bash = Find-Bash $BashPath
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-seed-dry-run-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    $envFile = Join-Path $tempDir ".env"
    Copy-Item -LiteralPath $exampleEnv -Destination $envFile
    Add-Content -LiteralPath $envFile -Value "COMPOSE_PROJECT_NAME=shifter-seed-dry-run" -Encoding ASCII

    $command = @(
        "cd '$(To-BashPath $ShifterDir)'",
        "&&",
        "DRY_RUN=1",
        "SHIFTER_DIR='$(To-BashPath $ShifterDir)'",
        "ENV_FILE='$(To-BashPath $envFile)'",
        "SEED_FILE='$(To-BashPath $seedFile)'",
        "COMPOSE_PROJECT_NAME=shifter-seed-dry-run",
        "bash '$(To-BashPath $seedScript)'"
    ) -join " "

    $output = & $bash -lc $command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "seed-compose dry run failed with exit $LASTEXITCODE. Output:`n$($output | Out-String)"
    }

    $text = $output | Out-String
    $expectedEnvPath = To-BashPath $envFile
    $expectedSeedPath = To-BashPath $seedFile
    if ($text -notmatch "Seed dry run passed" -or
        $text -notmatch [regex]::Escape($expectedEnvPath) -or
        $text -notmatch [regex]::Escape($expectedSeedPath) -or
        $text -notmatch "Project: shifter-seed-dry-run" -or
        $text -notmatch "Database: shifter" -or
        $text -notmatch "User: shifter") {
        throw "seed-compose dry run output did not include expected validation details. Output:`n$text"
    }

    Write-Host "Seed compose dry-run test passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
