param(
    [string]$ShifterDir = $(Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"

$generator = Join-Path $PSScriptRoot "generate-signed-license.ps1"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "shifter-license-generator-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempDir | Out-Null

function Get-CanonicalPayload {
    param(
        [string]$DeploymentMode,
        [string]$Licensee,
        [string]$LicenseKey,
        [string]$ExpiresAt
    )

    return @(
        "deploymentMode=$($DeploymentMode.Trim())",
        "licensee=$($Licensee.Trim())",
        "licenseKey=$($LicenseKey.Trim())",
        "expiresAt=$ExpiresAt"
    ) -join "`n"
}

try {
    $privateKey = Join-Path $tempDir "private.xml"
    $publicKey = Join-Path $tempDir "public.pem"
    $licensePath = Join-Path $tempDir "license.customer.json"
    $licensee = "Acme Scheduling Ltd"
    $licenseKey = "valid-customer-license-key-2026"
    $expiresAt = "2031-01-02T03:04:05Z"

    & $generator `
        -Licensee $licensee `
        -LicenseKey $licenseKey `
        -ExpiresAt $expiresAt `
        -PrivateKeyPath $privateKey `
        -PublicKeyPath $publicKey `
        -OutputPath $licensePath `
        -GenerateKeyPair

    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "generate-signed-license.ps1 failed with exit code $LASTEXITCODE."
    }

    foreach ($path in @($privateKey, $publicKey, $licensePath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Expected generated file was missing: $path"
        }
    }

    $license = Get-Content -Raw -LiteralPath $licensePath | ConvertFrom-Json
    if ($license.deploymentMode -ne "customer-hosted" -or
        $license.licensee -ne $licensee -or
        $license.licenseKey -ne $licenseKey -or
        [string]::IsNullOrWhiteSpace($license.signature)) {
        throw "Generated license content is invalid: $($license | ConvertTo-Json -Depth 4)"
    }

    $publicPem = Get-Content -Raw -LiteralPath $publicKey
    if ($publicPem -notmatch "BEGIN PUBLIC KEY") {
        throw "Generated public key is not PEM."
    }

    $rsa = [System.Security.Cryptography.RSA]::Create()
    try {
        $rsa.FromXmlString((Get-Content -Raw -LiteralPath $privateKey))
        $payload = Get-CanonicalPayload `
            -DeploymentMode $license.deploymentMode `
            -Licensee $license.licensee `
            -LicenseKey $license.licenseKey `
            -ExpiresAt ([DateTimeOffset]::Parse($license.expiresAt).UtcDateTime.ToString("O"))
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hash = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))
        }
        finally {
            $sha256.Dispose()
        }
        $valid = $rsa.VerifyHash(
            $hash,
            [System.Security.Cryptography.CryptoConfig]::MapNameToOID("SHA256"),
            [Convert]::FromBase64String($license.signature))

        if (-not $valid) {
            throw "Generated license signature did not verify with generated signing key."
        }
    }
    finally {
        $rsa.Dispose()
    }

    Write-Host "Signed license generator test passed." -ForegroundColor Green
}
finally {
    Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
