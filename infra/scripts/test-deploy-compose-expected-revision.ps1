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
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-deploy-revision-test-$([Guid]::NewGuid().ToString('N'))"
$fakeBin = Join-Path $tempDir "bin"
$tempShifterDir = Join-Path $tempDir "shifter"
$tempScriptsDir = Join-Path $tempShifterDir "infra\scripts"
$tempComposeDir = Join-Path $tempShifterDir "infra\compose"
New-Item -ItemType Directory -Path $fakeBin, $tempScriptsDir, $tempComposeDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $tempShifterDir ".git") | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "deploy-compose.sh") -Destination $tempScriptsDir

    $envFile = Join-Path $tempComposeDir ".env"
    $dockerLog = Join-Path $tempDir "docker.log"
    $gitLog = Join-Path $tempDir "git.log"
    $fakeDocker = Join-Path $fakeBin "docker"
    $fakeGit = Join-Path $fakeBin "git"
    $deployScript = Join-Path $tempScriptsDir "deploy-compose.sh"

    Set-Content -LiteralPath $envFile -Encoding ASCII -Value @(
        "COMPOSE_PROJECT_NAME=shifter-revision-test",
        "WEB_PORT=3015",
        "API_PORT=5015"
    )

    $fakeDockerContent = @'
#!/bin/bash
set -euo pipefail
echo "$@" >> "__DOCKER_LOG__"
exit 0
'@.Replace("__DOCKER_LOG__", (To-BashPath $dockerLog))
    Set-Content -LiteralPath $fakeDocker -Encoding ASCII -Value $fakeDockerContent

    $fakeGitContent = @'
#!/bin/bash
set -euo pipefail
echo "$@" >> "__GIT_LOG__"

case "${1:-}" in
  rev-parse)
    echo "actual-revision"
    ;;
  fetch|checkout|pull)
    exit 0
    ;;
  show-ref)
    exit 0
    ;;
  *)
    echo "unexpected git call: $*" >&2
    exit 1
    ;;
esac
'@.Replace("__GIT_LOG__", (To-BashPath $gitLog))
    Set-Content -LiteralPath $fakeGit -Encoding ASCII -Value $fakeGitContent

    $command = @(
        "chmod +x '$(To-BashPath $fakeDocker)'",
        "&&",
        "chmod +x '$(To-BashPath $fakeGit)'",
        "&&",
        "chmod +x '$(To-BashPath $deployScript)'",
        "&&",
        "PATH='$(To-BashPath $fakeBin)':`$PATH",
        "SHIFTER_DIR='$(To-BashPath $tempShifterDir)'",
        "COMPOSE_DIR='$(To-BashPath $tempComposeDir)'",
        "ENV_FILE='$(To-BashPath $envFile)'",
        "GIT_REF=develop",
        "EXPECTED_REVISION=expected-revision",
        "bash '$(To-BashPath $deployScript)'"
    ) -join " "

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $bash -lc "$command 2>&1"
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -eq 0) {
        throw "deploy-compose expected revision test should fail when revisions differ. Output:`n$($output | Out-String)"
    }

    $text = $output | Out-String
    if ($text -notmatch "Expected revision expected-revision, but develop resolved to actual-revision") {
        throw "deploy-compose output did not include the expected revision mismatch. Output:`n$text"
    }

    if (Test-Path -LiteralPath $dockerLog) {
        throw "Docker should not be called after an expected revision mismatch. Log:`n$(Get-Content -Raw -LiteralPath $dockerLog)"
    }

    Write-Host "Deploy compose expected revision test passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
