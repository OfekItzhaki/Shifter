param(
    [int]$PgPort = $(if ($env:SHIFTER_POSTGRES_IMPORT_SMOKE_PORT) { [int]$env:SHIFTER_POSTGRES_IMPORT_SMOKE_PORT } else { 55432 }),
    [string]$PostgresImage = $(if ($env:SHIFTER_POSTGRES_IMPORT_SMOKE_IMAGE) { $env:SHIFTER_POSTGRES_IMPORT_SMOKE_IMAGE } else { "postgres:16-alpine" }),
    [switch]$KeepContainer
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
$migrationsDir = Join-Path $repoRoot "infra\migrations"
$testProject = Join-Path $repoRoot "apps\api\Jobuler.Tests\Jobuler.Tests.csproj"

$containerName = "shifter-import-smoke-$([Guid]::NewGuid().ToString("N").Substring(0, 12))"
$dbName = "jobuler_import_smoke"
$dbUser = "jobuler_import_smoke"
$dbPassword = "jobuler_import_smoke_pass"
$connectionString = "Host=localhost;Port=$PgPort;Database=$dbName;Username=$dbUser;Password=$dbPassword"

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Docker {
    param([string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Wait-ForPostgres {
    for ($i = 1; $i -le 45; $i++) {
        & docker exec $containerName pg_isready -U $dbUser -d $dbName *> $null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL did not become ready in container $containerName."
}

try {
    Write-Step "Starting temporary PostgreSQL container $containerName on localhost:$PgPort"
    Invoke-Docker -Arguments @(
        "run",
        "-d",
        "--name", $containerName,
        "-e", "POSTGRES_DB=$dbName",
        "-e", "POSTGRES_USER=$dbUser",
        "-e", "POSTGRES_PASSWORD=$dbPassword",
        "-p", "${PgPort}:5432",
        $PostgresImage
    ) | Out-Null

    Write-Step "Waiting for PostgreSQL"
    Wait-ForPostgres

    Write-Step "Copying SQL migrations"
    Invoke-Docker -Arguments @("cp", "$migrationsDir/.", "${containerName}:/migrations")

    Write-Step "Applying SQL migrations"
    Invoke-Docker -Arguments @(
        "exec",
        "-e", "PGPASSWORD=$dbPassword",
        $containerName,
        "sh",
        "-c",
        "for file in `$(ls /migrations/*.sql | sort); do echo Applying `$(basename `$file); psql -U '$dbUser' -d '$dbName' -v ON_ERROR_STOP=1 -f `$file >/dev/null; done"
    )

    Write-Step "Running PostgreSQL organization import smoke"
    $previousConnection = $env:SHIFTER_POSTGRES_IMPORT_SMOKE_CONNECTION
    $env:SHIFTER_POSTGRES_IMPORT_SMOKE_CONNECTION = $connectionString
    try {
        $testOutput = & dotnet test $testProject --filter "FullyQualifiedName~PostgresImportOrganizationPackage" --logger "console;verbosity=minimal" 2>&1
        $testOutput | ForEach-Object { Write-Host $_ }
        $testExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
        if ($testExitCode -ne 0) {
            throw "PostgreSQL organization import smoke failed with exit code $testExitCode. Output:`n$($testOutput | Out-String)"
        }
    }
    finally {
        if ($null -eq $previousConnection) {
            Remove-Item Env:\SHIFTER_POSTGRES_IMPORT_SMOKE_CONNECTION -ErrorAction SilentlyContinue
        }
        else {
            $env:SHIFTER_POSTGRES_IMPORT_SMOKE_CONNECTION = $previousConnection
        }
    }

    Write-Host "PostgreSQL organization import smoke passed." -ForegroundColor Green
}
finally {
    if ($KeepContainer) {
        Write-Host "Keeping container $containerName because -KeepContainer was supplied." -ForegroundColor Yellow
    }
    else {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $removeOutput = & docker rm -f $containerName 2>&1
            if ($LASTEXITCODE -ne 0 -and ($removeOutput -notmatch "No such container")) {
                Write-Warning "Could not remove container $containerName. Output: $removeOutput"
            }
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
    }
}
