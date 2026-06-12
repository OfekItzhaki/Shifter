param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\..")),
    [string]$BashPath = ""
)

$ErrorActionPreference = "Stop"

$bundleScript = Join-Path $PSScriptRoot "bundle-compose-images.sh"

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
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-bundle-test-$([Guid]::NewGuid().ToString('N'))"
$fakeBin = Join-Path $tempDir "bin"
$bundleDir = Join-Path $tempDir "bundle"
New-Item -ItemType Directory -Path $fakeBin, $bundleDir | Out-Null

try {
    $envFile = Join-Path $tempDir ".env"
    $dockerLog = Join-Path $tempDir "docker.log"
    $fakeDocker = Join-Path $fakeBin "docker"
    $fakeDockerExe = Join-Path $fakeBin "docker.exe"

    Set-Content -LiteralPath $envFile -Encoding ASCII -Value @(
        "COMPOSE_PROJECT_NAME=shifter-bundle-test",
        "POSTGRES_DB=shifter",
        "POSTGRES_USER=shifter",
        "POSTGRES_PASSWORD=secret",
        "REDIS_PASSWORD=secret",
        "MINIO_ROOT_USER=shifter",
        "MINIO_ROOT_PASSWORD=secret",
        "SEQ_ADMIN_PASSWORD=secret"
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
      -f)
        echo "compose-file=$2" >> "__DOCKER_LOG__"
        shift 2
        ;;
      build)
        echo "build=${*:2}" >> "__DOCKER_LOG__"
        exit 0
        ;;
      pull)
        echo "pull=${*:2}" >> "__DOCKER_LOG__"
        exit 0
        ;;
      config)
        if [ "${2:-}" = "--images" ]; then
          printf '%s\n' \
            "postgres:16-alpine" \
            "redis:7-alpine" \
            "minio/minio:RELEASE.2024-06-13T22-53-53Z" \
            "shifter-api:shifter-bundle-test" \
            "shifter-solver:shifter-bundle-test" \
            "shifter-web:shifter-bundle-test" \
            "datalust/seq:2024.3"
          exit 0
        fi
        ;;
      *)
        shift
        ;;
    esac
  done
fi

if [ "${1:-}" = "save" ]; then
  shift
  out=""
  while [ "$#" -gt 0 ]; do
    case "$1" in
      -o)
        out="$2"
        shift 2
        ;;
      *)
        echo "save-image=$1" >> "__DOCKER_LOG__"
        shift
        ;;
    esac
  done
  printf 'fake image bundle\n' > "$out"
  exit 0
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
        "ENV_FILE='$(To-BashPath $envFile)'",
        "BUNDLE_DIR='$(To-BashPath $bundleDir)'",
        "bash '$(To-BashPath $bundleScript)'"
    ) -join " "

    $output = & $bash -lc $command 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "bundle-compose-images test failed with exit $LASTEXITCODE. Output:`n$($output | Out-String)"
    }

    $bundle = Join-Path $bundleDir "shifter-shifter-bundle-test-images.tar"
    $manifest = "$bundle.manifest.txt"
    $sha = "$bundle.sha256"
    foreach ($path in @($bundle, $manifest, $sha)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Expected bundle artifact was not created: $path"
        }
    }

    $manifestText = Get-Content -Raw -LiteralPath $manifest
    foreach ($image in @(
            "shifter-api:shifter-bundle-test",
            "shifter-solver:shifter-bundle-test",
            "shifter-web:shifter-bundle-test",
            "postgres:16-alpine",
            "redis:7-alpine",
            "minio/minio:RELEASE.2024-06-13T22-53-53Z",
            "datalust/seq:2024.3"
        )) {
        if ($manifestText -notmatch [regex]::Escape($image)) {
            throw "Bundle manifest is missing $image. Manifest:`n$manifestText"
        }
    }

    $log = Get-Content -Raw -LiteralPath $dockerLog
    if ($log -notmatch "build=api solver web" -or
        $log -notmatch "pull=postgres redis minio seq" -or
        $log -notmatch "save-image=shifter-api:shifter-bundle-test") {
        throw "Fake docker log did not show expected build/pull/save calls. Log:`n$log"
    }

    Write-Host "Bundle compose images test passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
