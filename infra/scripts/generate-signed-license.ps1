param(
    [Parameter(Mandatory = $true)]
    [string]$Licensee,

    [Parameter(Mandatory = $true)]
    [string]$LicenseKey,

    [string]$ExpiresAt = "",
    [string]$PrivateKeyPath = "",
    [string]$PublicKeyPath = "",
    [string]$OutputPath = "",
    [switch]$GenerateKeyPair,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $safeName = ($Licensee -replace '[^A-Za-z0-9_-]+', '-').Trim('-').ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($safeName)) {
        $safeName = "customer"
    }
    $OutputPath = Join-Path (Get-Location) "license.$safeName.json"
}

if ([string]::IsNullOrWhiteSpace($PrivateKeyPath)) {
    $PrivateKeyPath = Join-Path (Get-Location) "shifter-license-private.xml"
}

if ([string]::IsNullOrWhiteSpace($PublicKeyPath)) {
    $PublicKeyPath = Join-Path (Get-Location) "shifter-license-public.pem"
}

function Assert-CanWrite {
    param([string]$Path)

    if ((Test-Path -LiteralPath $Path) -and -not $Force) {
        throw "$Path already exists. Pass -Force to overwrite it."
    }

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
}

function Normalize-Pem {
    param([string]$Pem)
    return $Pem.Replace("\n", "`n")
}

function Join-Bytes {
    param([byte[][]]$Parts)

    $length = 0
    foreach ($part in $Parts) {
        $length += $part.Length
    }

    $result = New-Object byte[] $length
    $offset = 0
    foreach ($part in $Parts) {
        [Array]::Copy($part, 0, $result, $offset, $part.Length)
        $offset += $part.Length
    }

    return $result
}

function New-Asn1Length {
    param([int]$Length)

    if ($Length -lt 128) {
        return [byte[]]@($Length)
    }

    $bytes = New-Object System.Collections.Generic.List[byte]
    $remaining = $Length
    while ($remaining -gt 0) {
        $bytes.Insert(0, [byte]($remaining -band 0xff))
        $remaining = $remaining -shr 8
    }

    return [byte[]]@([byte](0x80 -bor $bytes.Count)) + $bytes.ToArray()
}

function New-Asn1Element {
    param(
        [byte]$Tag,
        [byte[]]$Value
    )

    return Join-Bytes @([byte[]]@($Tag), (New-Asn1Length $Value.Length), $Value)
}

function New-Asn1Integer {
    param([byte[]]$Value)

    $start = 0
    while ($start -lt ($Value.Length - 1) -and $Value[$start] -eq 0) {
        $start++
    }

    $trimmed = New-Object byte[] ($Value.Length - $start)
    [Array]::Copy($Value, $start, $trimmed, 0, $trimmed.Length)
    if (($trimmed[0] -band 0x80) -ne 0) {
        $trimmed = [byte[]]@(0) + $trimmed
    }

    return New-Asn1Element 0x02 $trimmed
}

function New-Asn1Sequence {
    param([byte[][]]$Parts)
    return New-Asn1Element 0x30 (Join-Bytes $Parts)
}

function New-Asn1BitString {
    param([byte[]]$Value)
    return New-Asn1Element 0x03 ([byte[]]@(0) + $Value)
}

function Convert-PublicKeyToSubjectPublicKeyInfo {
    param([System.Security.Cryptography.RSAParameters]$Parameters)

    $rsaPublicKey = New-Asn1Sequence @(
        (New-Asn1Integer $Parameters.Modulus),
        (New-Asn1Integer $Parameters.Exponent)
    )
    $algorithm = New-Asn1Sequence @(
        [byte[]]@(0x06, 0x09, 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01),
        [byte[]]@(0x05, 0x00)
    )
    return New-Asn1Sequence @($algorithm, (New-Asn1BitString $rsaPublicKey))
}

function Convert-DerToPem {
    param(
        [byte[]]$Der,
        [string]$Label
    )

    $base64 = [Convert]::ToBase64String($Der)
    $lines = for ($i = 0; $i -lt $base64.Length; $i += 64) {
        $length = [Math]::Min(64, $base64.Length - $i)
        $base64.Substring($i, $length)
    }

    return @(
        "-----BEGIN $Label-----",
        $lines,
        "-----END $Label-----"
    ) -join "`n"
}

function Get-CanonicalPayload {
    param(
        [string]$DeploymentMode,
        [string]$LicenseeValue,
        [string]$LicenseKeyValue,
        [AllowNull()][DateTimeOffset]$Expiration
    )

    $expiresValue = if ($null -eq $Expiration) { "" } else { $Expiration.UtcDateTime.ToString("O") }
    return @(
        "deploymentMode=$($DeploymentMode.Trim())",
        "licensee=$($LicenseeValue.Trim())",
        "licenseKey=$($LicenseKeyValue.Trim())",
        "expiresAt=$expiresValue"
    ) -join "`n"
}

function Invoke-RsaSignHash {
    param(
        [System.Security.Cryptography.RSA]$Rsa,
        [byte[]]$Hash
    )

    try {
        return $Rsa.SignHash(
            $Hash,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    }
    catch [System.Management.Automation.MethodException] {
        return $Rsa.SignHash($Hash, [System.Security.Cryptography.CryptoConfig]::MapNameToOID("SHA256"))
    }
}

if ($LicenseKey.Trim().Length -lt 24) {
    throw "LicenseKey must be at least 24 characters."
}

$expiration = $null
if (-not [string]::IsNullOrWhiteSpace($ExpiresAt)) {
    $expiration = [DateTimeOffset]::Parse($ExpiresAt).ToUniversalTime()
}

if ($GenerateKeyPair) {
    Assert-CanWrite $PrivateKeyPath
    Assert-CanWrite $PublicKeyPath

    $keyPair = [System.Security.Cryptography.RSA]::Create(3072)
    try {
        Set-Content -LiteralPath $PrivateKeyPath -Value $keyPair.ToXmlString($true) -Encoding ASCII
        $publicDer = Convert-PublicKeyToSubjectPublicKeyInfo $keyPair.ExportParameters($false)
        Set-Content -LiteralPath $PublicKeyPath -Value (Convert-DerToPem $publicDer "PUBLIC KEY") -Encoding ASCII
    }
    finally {
        $keyPair.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $PrivateKeyPath)) {
    throw "Private key file not found: $PrivateKeyPath. Pass -GenerateKeyPair to create one."
}

Assert-CanWrite $OutputPath

$rsa = [System.Security.Cryptography.RSA]::Create()
try {
    $privateKeyText = Normalize-Pem (Get-Content -Raw -LiteralPath $PrivateKeyPath)
    if ($privateKeyText -match '<RSAKeyValue>') {
        $rsa.FromXmlString($privateKeyText)
    }
    else {
        throw "Unsupported private key format. This script expects the XML private key generated by -GenerateKeyPair."
    }
    $deploymentMode = "customer-hosted"
    $payload = Get-CanonicalPayload `
        -DeploymentMode $deploymentMode `
        -LicenseeValue $Licensee `
        -LicenseKeyValue $LicenseKey `
        -Expiration $expiration
    $payloadBytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($payloadBytes)
    }
    finally {
        $sha256.Dispose()
    }
    $signature = [Convert]::ToBase64String((Invoke-RsaSignHash $rsa $hash))

    $license = [ordered]@{
        deploymentMode = $deploymentMode
        licensee = $Licensee.Trim()
        licenseKey = $LicenseKey.Trim()
        expiresAt = if ($null -eq $expiration) { $null } else { $expiration.UtcDateTime.ToString("O") }
        signature = $signature
    }

    Set-Content -LiteralPath $OutputPath -Value ($license | ConvertTo-Json -Depth 4) -Encoding ASCII
}
finally {
    $rsa.Dispose()
}

Write-Host "Signed license written: $OutputPath" -ForegroundColor Green
Write-Host "Private signing key: $PrivateKeyPath"
Write-Host "Public verification key: $PublicKeyPath"
