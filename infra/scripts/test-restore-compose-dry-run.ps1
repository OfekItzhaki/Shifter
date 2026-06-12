param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = ""
)

$ErrorActionPreference = "Stop"

$restoreScript = Join-Path $PSScriptRoot "restore-compose.sh"
$exampleEnv = Join-Path $ShifterDir "infra\compose\.env.customer.example"

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
    return ($Path -replace '\\', '/')
}

$bash = Find-Bash $BashPath
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-restore-dry-run-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    $envFile = Join-Path $tempDir ".env"
    $dbBackup = Join-Path $tempDir "postgres.dump"
    Copy-Item -LiteralPath $exampleEnv -Destination $envFile
    Add-Content -LiteralPath $envFile -Value "COMPOSE_PROJECT_NAME=shifter-restore-dry-run" -Encoding ASCII
    Set-Content -LiteralPath $dbBackup -Value "not-a-real-dump-but-non-empty-for-dry-run" -Encoding ASCII

    $command = @(
        "cd '$(To-BashPath $ShifterDir)'",
        "&&",
        "DRY_RUN=1",
        "SHIFTER_DIR='$(To-BashPath $ShifterDir)'",
        "ENV_FILE='$(To-BashPath $envFile)'",
        "DB_BACKUP='$(To-BashPath $dbBackup)'",
        "bash '$(To-BashPath $restoreScript)'"
    ) -join " "

    $output = & $bash -lc $command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "restore-compose dry run failed with exit $LASTEXITCODE. Output:`n$($output | Out-String)"
    }

    $text = $output | Out-String
    $expectedEnvPath = To-BashPath $envFile
    if ($text -notmatch "Restore dry run passed" -or
        $text -notmatch "Compose config is valid" -or
        $text -notmatch [regex]::Escape($expectedEnvPath)) {
        throw "restore-compose dry run output did not include expected validation details. Output:`n$text"
    }

    Write-Host "Restore compose dry-run test passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
