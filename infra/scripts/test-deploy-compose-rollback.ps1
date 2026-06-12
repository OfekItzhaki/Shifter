param(
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

function To-BashPath {
    param([string]$Path)
    $normalized = $Path -replace '\\', '/'
    if ($normalized -match '^([A-Za-z]):/(.*)$') {
        return "/$($Matches[1].ToLowerInvariant())/$($Matches[2])"
    }
    return $normalized
}

$bash = Find-Bash $BashPath
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-deploy-rollback-test-$([Guid]::NewGuid().ToString('N'))"
$fakeBin = Join-Path $tempDir "bin"
$tempShifterDir = Join-Path $tempDir "shifter"
$tempScriptsDir = Join-Path $tempShifterDir "infra\scripts"
$tempComposeDir = Join-Path $tempShifterDir "infra\compose"
$backupDir = Join-Path $tempDir "backups"
New-Item -ItemType Directory -Path $fakeBin, $tempScriptsDir, $tempComposeDir, $backupDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempShifterDir ".git") | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "deploy-compose.sh") -Destination $tempScriptsDir
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "backup-compose.sh") -Destination $tempScriptsDir

    $envFile = Join-Path $tempComposeDir ".env"
    $dockerLog = Join-Path $tempDir "docker.log"
    $gitLog = Join-Path $tempDir "git.log"
    $curlLog = Join-Path $tempDir "curl.log"
    $gitRevState = Join-Path $tempDir "git-rev-state"
    $rollbackMarker = Join-Path $tempDir "rollback-complete"
    $fakeDocker = Join-Path $fakeBin "docker"
    $fakeDockerExe = Join-Path $fakeBin "docker.exe"
    $fakeGit = Join-Path $fakeBin "git"
    $fakeGitExe = Join-Path $fakeBin "git.exe"
    $fakeCurl = Join-Path $fakeBin "curl"
    $fakeCurlExe = Join-Path $fakeBin "curl.exe"
    $deployScript = Join-Path $tempScriptsDir "deploy-compose.sh"
    $backupScript = Join-Path $tempScriptsDir "backup-compose.sh"

    Set-Content -LiteralPath $envFile -Encoding ASCII -Value @(
        "COMPOSE_PROJECT_NAME=shifter-rollback-test",
        "POSTGRES_DB=shifterdb",
        "POSTGRES_USER=shifteruser",
        "WEB_PORT=3015",
        "API_PORT=5015"
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
      *)
        break
        ;;
    esac
  done

  command="${1:-}"
  shift || true
  case "$command" in
    ps)
      if printf '%s\n' "$@" | grep -qx postgres; then
        echo "postgres"
      elif printf '%s\n' "$@" | grep -qx json; then
        echo "[]"
      else
        echo "NAME STATUS"
      fi
      exit 0
      ;;
    up)
      echo "compose-up $*" >> "__DOCKER_LOG__"
      exit 0
      ;;
    exec)
      if [ "${1:-}" = "-T" ]; then shift; fi
      service="${1:-}"
      shift || true
      if [ "$service" = "postgres" ] && [ "${1:-}" = "pg_dump" ]; then
        echo "fake custom dump"
        exit 0
      fi
      ;;
    logs)
      echo "fake api web solver logs"
      exit 0
      ;;
  esac
fi

if [ "${1:-}" = "volume" ] && [ "${2:-}" = "inspect" ]; then
  exit 1
fi

echo "unexpected docker call: $*" >&2
exit 1
'@.Replace("__DOCKER_LOG__", (To-BashPath $dockerLog))
    Set-Content -LiteralPath $fakeDocker -Encoding ASCII -Value $fakeDockerContent
    Set-Content -LiteralPath $fakeDockerExe -Encoding ASCII -Value $fakeDockerContent

    $fakeGitContent = @'
#!/bin/bash
set -euo pipefail
echo "$@" >> "__GIT_LOG__"

case "${1:-}" in
  rev-parse)
    if [ -f "__GIT_REV_STATE__" ]; then
      echo "new-revision"
    else
      echo "previous-revision"
      touch "__GIT_REV_STATE__"
    fi
    ;;
  fetch|pull)
    exit 0
    ;;
  show-ref)
    exit 0
    ;;
  checkout)
    if [ "${2:-}" = "--detach" ] && [ "${3:-}" = "previous-revision" ]; then
      touch "__ROLLBACK_MARKER__"
    fi
    exit 0
    ;;
  *)
    echo "unexpected git call: $*" >&2
    exit 1
    ;;
esac
'@.
        Replace("__GIT_LOG__", (To-BashPath $gitLog)).
        Replace("__GIT_REV_STATE__", (To-BashPath $gitRevState)).
        Replace("__ROLLBACK_MARKER__", (To-BashPath $rollbackMarker))
    Set-Content -LiteralPath $fakeGit -Encoding ASCII -Value $fakeGitContent
    Set-Content -LiteralPath $fakeGitExe -Encoding ASCII -Value $fakeGitContent

    $fakeCurlContent = @'
#!/bin/bash
set -euo pipefail
echo "$@" >> "__CURL_LOG__"
if [ -f "__ROLLBACK_MARKER__" ]; then
  exit 0
fi
exit 1
'@.
        Replace("__CURL_LOG__", (To-BashPath $curlLog)).
        Replace("__ROLLBACK_MARKER__", (To-BashPath $rollbackMarker))
    Set-Content -LiteralPath $fakeCurl -Encoding ASCII -Value $fakeCurlContent
    Set-Content -LiteralPath $fakeCurlExe -Encoding ASCII -Value $fakeCurlContent

    $command = @(
        "chmod +x '$(To-BashPath $fakeDocker)'",
        "&&",
        "chmod +x '$(To-BashPath $fakeDockerExe)'",
        "&&",
        "chmod +x '$(To-BashPath $fakeGit)'",
        "&&",
        "chmod +x '$(To-BashPath $fakeGitExe)'",
        "&&",
        "chmod +x '$(To-BashPath $fakeCurl)'",
        "&&",
        "chmod +x '$(To-BashPath $fakeCurlExe)'",
        "&&",
        "chmod +x '$(To-BashPath $deployScript)'",
        "&&",
        "chmod +x '$(To-BashPath $backupScript)'",
        "&&",
        "PATH='$(To-BashPath $fakeBin)':`$PATH",
        "SHIFTER_DIR='$(To-BashPath $tempShifterDir)'",
        "COMPOSE_DIR='$(To-BashPath $tempComposeDir)'",
        "ENV_FILE='$(To-BashPath $envFile)'",
        "BACKUP_DIR='$(To-BashPath $backupDir)'",
        "GIT_REF=main",
        "HEALTH_TIMEOUT_SECONDS=1",
        "bash '$(To-BashPath $deployScript)'"
    ) -join " "

    $output = & $bash -lc "$command 2>&1"
    if ($LASTEXITCODE -ne 1) {
        throw "deploy-compose rollback test expected exit 1, got $LASTEXITCODE. Output:`n$($output | Out-String)"
    }

    $text = $output | Out-String
    if ($text -notmatch "Deploy failed health verification" -or
        $text -notmatch "Rolling back to previous-revision" -or
        $text -notmatch "Rollback healthy: previous-revision") {
        throw "deploy-compose rollback output did not include expected failure and rollback messages. Output:`n$text"
    }

    if (-not (Test-Path -LiteralPath $rollbackMarker)) {
        throw "Expected rollback marker to be created by git checkout --detach previous-revision."
    }

    $dumps = Get-ChildItem -LiteralPath $backupDir -Filter "postgres_shifter-rollback-test_*.dump"
    if ($dumps.Count -ne 1 -or $dumps[0].Length -le 0) {
        throw "Expected one non-empty pre-deploy postgres backup in $backupDir."
    }

    $dockerText = Get-Content -Raw -LiteralPath $dockerLog
    $upCount = ([regex]::Matches($dockerText, "compose-up -d --build --remove-orphans")).Count
    if ($upCount -ne 2) {
        throw "Expected Compose up to run once for deploy and once for rollback. Log:`n$dockerText"
    }

    $gitText = Get-Content -Raw -LiteralPath $gitLog
    if ($gitText -notmatch "checkout --detach previous-revision") {
        throw "Fake git log did not show rollback checkout. Log:`n$gitText"
    }

    $curlText = Get-Content -Raw -LiteralPath $curlLog
    if ($curlText -notmatch "http://127\.0\.0\.1:3015" -or
        $curlText -notmatch "http://127\.0\.0\.1:5015/health") {
        throw "Fake curl log did not show expected web/API health checks. Log:`n$curlText"
    }

    Write-Host "Deploy compose rollback test passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
