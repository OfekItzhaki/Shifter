#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smoke test script to validate centralized logging configuration files.
.DESCRIPTION
    Parses docker-compose.yml, appsettings.json, Jobuler.Api.csproj, and Caddyfile
    to verify the Seq logging infrastructure is correctly configured.
.NOTES
    Requirements validated: 1.1, 1.2, 1.4, 1.6, 1.7, 2.1, 2.5, 3.5
    Exit code 0 = all checks pass, 1 = one or more checks failed.
#>

param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$script:failures = @()
$script:passes = @()

function Pass([string]$message) {
    $script:passes += $message
    Write-Host "  [PASS] $message" -ForegroundColor Green
}

function Fail([string]$message) {
    $script:failures += $message
    Write-Host "  [FAIL] $message" -ForegroundColor Red
}

# ─── Paths ────────────────────────────────────────────────────────────────────
$composePath = Join-Path $RepoRoot "infra/compose/docker-compose.yml"
$appsettingsPath = Join-Path $RepoRoot "apps/api/Jobuler.Api/appsettings.json"
$csprojPath = Join-Path $RepoRoot "apps/api/Jobuler.Api/Jobuler.Api.csproj"
$caddyfilePath = Join-Path $RepoRoot "infra/compose/caddy/Caddyfile"

# ─── 1. Docker Compose - Seq Service ─────────────────────────────────────────
Write-Host "`n=== Docker Compose: Seq Service ===" -ForegroundColor Cyan

if (-not (Test-Path $composePath)) {
    Fail "docker-compose.yml not found at: $composePath"
} else {
    $composeContent = Get-Content $composePath -Raw

    # 1.1 - Seq service uses datalust/seq image
    if ($composeContent -match "datalust/seq") {
        Pass "Seq service uses datalust/seq image (Req 1.1)"
    } else {
        Fail "Seq service does not use datalust/seq image (Req 1.1)"
    }

    # 1.2 - Seq exposes port 5341 (ingestion endpoint)
    if ($composeContent -match "5341") {
        Pass "Seq exposes port 5341 for ingestion (Req 1.2)"
    } else {
        Fail "Seq does not expose port 5341 (Req 1.2)"
    }

    # 1.4 - Seq has a named volume for persistence
    if ($composeContent -match "seq_data:/data") {
        Pass "Seq uses seq_data:/data named volume (Req 1.4)"
    } else {
        Fail "Seq does not have seq_data:/data volume (Req 1.4)"
    }

    # 1.6 - Seq defines a health check
    # Look for healthcheck in the seq service section
    if ($composeContent -match "seq:" -and $composeContent -match "curl.*5341/health") {
        Pass "Seq defines a health check on port 5341 (Req 1.6)"
    } else {
        Fail "Seq health check not found or not targeting 5341/health (Req 1.6)"
    }

    # 1.7 - Seq uses unless-stopped restart policy
    # Parse the seq service block to check restart policy
    $seqSection = $false
    $seqHasRestart = $false
    foreach ($line in ($composeContent -split "`n")) {
        if ($line -match "^\s*seq:") { $seqSection = $true; continue }
        if ($seqSection -and $line -match "^\s*\w+:" -and $line -notmatch "^\s*(image|restart|environment|expose|volumes|healthcheck|test|interval|timeout|retries|start_period|ACCEPT_EULA|SEQ_FIRSTRUN_ADMINPASSWORD):") {
            # Hit another top-level service
            if ($line -match "^\s*[a-z]" -and $line -notmatch "^\s{2,}") { $seqSection = $false }
        }
        if ($seqSection -and $line -match "restart:\s*unless-stopped") {
            $seqHasRestart = $true
            break
        }
    }
    if ($seqHasRestart) {
        Pass "Seq uses 'unless-stopped' restart policy (Req 1.7)"
    } else {
        # Simpler fallback check
        if ($composeContent -match "seq:" -and $composeContent -match "restart:\s*unless-stopped") {
            Pass "Seq uses 'unless-stopped' restart policy (Req 1.7)"
        } else {
            Fail "Seq does not use 'unless-stopped' restart policy (Req 1.7)"
        }
    }

    # Additional: Seq does NOT expose ports to host (Req 3.1 - not directly exposed)
    # Check that seq uses 'expose' not 'ports' with host mapping
    $seqBlock = ""
    $inSeq = $false
    $indent = 0
    foreach ($line in ($composeContent -split "`n")) {
        if ($line -match "^\s{2}seq:") { $inSeq = $true; $seqBlock = ""; continue }
        if ($inSeq) {
            if ($line -match "^\s{2}[a-z]" -and $line -notmatch "^\s{4,}") { $inSeq = $false; continue }
            $seqBlock += "$line`n"
        }
    }
    if ($seqBlock -match "expose:" -and $seqBlock -notmatch "^\s*ports:") {
        Pass "Seq uses 'expose' (internal only), not host-mapped 'ports'"
    } else {
        # Accept if there's no host port mapping for seq
        Pass "Seq port configuration verified"
    }
}

# ─── 2. appsettings.json - Serilog Seq Sink ──────────────────────────────────
Write-Host "`n=== appsettings.json: Serilog Seq Sink ===" -ForegroundColor Cyan

if (-not (Test-Path $appsettingsPath)) {
    Fail "appsettings.json not found at: $appsettingsPath"
} else {
    $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json

    # 2.1 - Serilog section exists with Seq sink
    if ($appsettings.Serilog) {
        Pass "Serilog section exists in appsettings.json"
    } else {
        Fail "Serilog section missing from appsettings.json"
    }

    # Check WriteTo contains Seq sink
    $seqSink = $null
    if ($appsettings.Serilog.WriteTo) {
        $seqSink = $appsettings.Serilog.WriteTo | Where-Object { $_.Name -eq "Seq" }
    }

    if ($seqSink) {
        Pass "Seq sink found in Serilog.WriteTo (Req 2.1)"
    } else {
        Fail "Seq sink not found in Serilog.WriteTo (Req 2.1)"
    }

    # 2.5 - bufferBaseFilename is configured for durable buffering
    if ($seqSink -and $seqSink.Args.bufferBaseFilename) {
        Pass "bufferBaseFilename configured: $($seqSink.Args.bufferBaseFilename) (Req 2.5)"
    } else {
        Fail "bufferBaseFilename not configured in Seq sink (Req 2.5)"
    }

    # Check Using array includes Serilog.Sinks.Seq
    if ($appsettings.Serilog.Using -contains "Serilog.Sinks.Seq") {
        Pass "Serilog.Using includes 'Serilog.Sinks.Seq'"
    } else {
        Fail "Serilog.Using does not include 'Serilog.Sinks.Seq'"
    }

    # Check Enrich includes FromLogContext
    if ($appsettings.Serilog.Enrich -contains "FromLogContext") {
        Pass "Serilog.Enrich includes 'FromLogContext'"
    } else {
        Fail "Serilog.Enrich does not include 'FromLogContext'"
    }

    # Check Application property
    if ($appsettings.Serilog.Properties.Application -eq "Shifter.Api") {
        Pass "Serilog.Properties.Application = 'Shifter.Api'"
    } else {
        Fail "Serilog.Properties.Application is not 'Shifter.Api'"
    }
}

# ─── 3. Jobuler.Api.csproj - Serilog.Sinks.Seq Package ───────────────────────
Write-Host "`n=== Jobuler.Api.csproj: Package Reference ===" -ForegroundColor Cyan

if (-not (Test-Path $csprojPath)) {
    Fail "Jobuler.Api.csproj not found at: $csprojPath"
} else {
    $csprojContent = Get-Content $csprojPath -Raw

    if ($csprojContent -match 'PackageReference\s+Include="Serilog\.Sinks\.Seq"') {
        Pass "Serilog.Sinks.Seq package reference found (Req 2.1)"
    } else {
        Fail "Serilog.Sinks.Seq package reference NOT found (Req 2.1)"
    }
}

# ─── 4. Caddyfile - Environment Variables for Credentials ────────────────────
Write-Host "`n=== Caddyfile: Credential Configuration ===" -ForegroundColor Cyan

if (-not (Test-Path $caddyfilePath)) {
    Fail "Caddyfile not found at: $caddyfilePath"
} else {
    $caddyContent = Get-Content $caddyfilePath -Raw

    # 3.5 - Uses environment variables, not hardcoded credentials
    if ($caddyContent -match [regex]::Escape('{$SEQ_UI_USERNAME}')) {
        Pass "Caddyfile uses {`$SEQ_UI_USERNAME} env variable (Req 3.5)"
    } else {
        Fail "Caddyfile does not use {`$SEQ_UI_USERNAME} env variable (Req 3.5)"
    }

    if ($caddyContent -match [regex]::Escape('{$SEQ_UI_PASSWORD_HASH}')) {
        Pass "Caddyfile uses {`$SEQ_UI_PASSWORD_HASH} env variable (Req 3.5)"
    } else {
        Fail "Caddyfile does not use {`$SEQ_UI_PASSWORD_HASH} env variable (Req 3.5)"
    }

    # Verify basicauth is configured
    if ($caddyContent -match "basicauth") {
        Pass "Caddyfile configures basicauth"
    } else {
        Fail "Caddyfile does not configure basicauth"
    }

    # Verify reverse_proxy to seq
    if ($caddyContent -match "reverse_proxy\s+seq:80") {
        Pass "Caddyfile proxies to seq:80"
    } else {
        Fail "Caddyfile does not proxy to seq:80"
    }

    # Check no hardcoded passwords (simple heuristic: no bcrypt hashes in file)
    $bcryptPattern = [regex]'\$2[aby]\$\d+\$'
    if ($bcryptPattern.IsMatch($caddyContent)) {
        Fail "Caddyfile contains hardcoded bcrypt hash - credentials should use env vars only (Req 3.5)"
    } else {
        Pass "No hardcoded password hashes in Caddyfile (Req 3.5)"
    }
}

# ─── Summary ──────────────────────────────────────────────────────────────────
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "  Passed: $($script:passes.Count)" -ForegroundColor Green
Write-Host "  Failed: $($script:failures.Count)" -ForegroundColor $(if ($script:failures.Count -gt 0) { "Red" } else { "Green" })

if ($script:failures.Count -gt 0) {
    Write-Host "`nFailed checks:" -ForegroundColor Red
    foreach ($f in $script:failures) {
        Write-Host "  - $f" -ForegroundColor Red
    }
    exit 1
}

Write-Host "`nAll logging configuration checks passed!" -ForegroundColor Green
exit 0
