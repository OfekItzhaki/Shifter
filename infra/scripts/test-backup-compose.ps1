param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = ""
)

$ErrorActionPreference = "Stop"

$backupScript = Join-Path $PSScriptRoot "backup-compose.sh"

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
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-backup-test-$([Guid]::NewGuid().ToString('N'))"
$fakeBin = Join-Path $tempDir "bin"
$backupDir = Join-Path $tempDir "backups"
New-Item -ItemType Directory -Path $fakeBin, $backupDir | Out-Null

try {
    $envFile = Join-Path $tempDir ".env"
    $dockerLog = Join-Path $tempDir "docker.log"
    $fakeDocker = Join-Path $fakeBin "docker"
    $fakeDockerExe = Join-Path $fakeBin "docker.exe"

    Set-Content -LiteralPath $envFile -Encoding ASCII -Value @(
        "COMPOSE_PROJECT_NAME=shifter-backup-test",
        "POSTGRES_DB=shifterdb",
        "POSTGRES_USER=shifteruser"
    )

    $fakeDockerContent = @'
#!/bin/bash
set -euo pipefail
echo "$@" >> "__DOCKER_LOG__"

if [ "${1:-}" = "compose" ]; then
  shift
  while [ "$#" -gt 0 ]; do
    case "$1" in
      --env-file)
        echo "env-file=$2" >> "__DOCKER_LOG__"
        shift 2
        ;;
      --project-name)
        echo "project=$2" >> "__DOCKER_LOG__"
        shift 2
        ;;
      exec)
        shift
        if [ "${1:-}" = "-T" ]; then shift; fi
        service="${1:-}"
        shift
        if [ "$service" = "postgres" ] && [ "${1:-}" = "pg_dump" ]; then
          echo "fake custom dump"
          exit 0
        fi
        ;;
      *)
        shift
        ;;
    esac
  done
fi

if [ "${1:-}" = "volume" ] && [ "${2:-}" = "inspect" ]; then
  exit 1
fi

echo "unexpected docker call: $*" >&2
exit 1
'@.Replace("__DOCKER_LOG__", (To-BashPath $dockerLog))
    Set-Content -LiteralPath $fakeDocker -Encoding ASCII -Value $fakeDockerContent
    Set-Content -LiteralPath $fakeDockerExe -Encoding ASCII -Value $fakeDockerContent

    $command = @(
        "chmod +x '$(To-BashPath $fakeDocker)'",
        "&&",
        "chmod +x '$(To-BashPath $fakeDockerExe)'",
        "&&",
        "cd '$(To-BashPath $ShifterDir)'",
        "&&",
        "PATH='$(To-BashPath $fakeBin)':`$PATH",
        "SHIFTER_DIR='$(To-BashPath $ShifterDir)'",
        "COMPOSE_DIR='$(To-BashPath (Join-Path $ShifterDir "infra\compose"))'",
        "ENV_FILE='$(To-BashPath $envFile)'",
        "BACKUP_DIR='$(To-BashPath $backupDir)'",
        "RETENTION_DAYS=7",
        "bash '$(To-BashPath $backupScript)'"
    ) -join " "

    $output = & $bash -lc $command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "backup-compose test failed with exit $LASTEXITCODE. Output:`n$($output | Out-String)"
    }

    $dumps = Get-ChildItem -LiteralPath $backupDir -Filter "postgres_shifter-backup-test_*.dump"
    if ($dumps.Count -ne 1 -or $dumps[0].Length -le 0) {
        throw "Expected one non-empty postgres backup in $backupDir."
    }

    $log = Get-Content -Raw -LiteralPath $dockerLog
    $expectedEnvPath = To-BashPath $envFile
    if ($log -notmatch [regex]::Escape("env-file=$expectedEnvPath") -or
        $log -notmatch "project=shifter-backup-test") {
        throw "Fake docker log did not show expected env/project arguments. Log:`n$log"
    }

    Write-Host "Backup compose test passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
