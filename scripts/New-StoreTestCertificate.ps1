[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts\store-test-certificate',

    [ValidateRange(1, 10)]
    [int]$ValidityYears = 3,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publisher = 'CN=33B5F0AF-2704-46FB-8180-E63B444C2020'

if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    $outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$pfxPath = Join-Path $outputRoot 'OpenTrace-Test.pfx'
$cerPath = Join-Path $outputRoot 'OpenTrace-Test.cer'
$passwordPath = Join-Path $outputRoot 'OpenTrace-Test.password.clixml'

$certificate = $null
if (!$Force) {
    $certificate = Get-ChildItem -LiteralPath 'Cert:\CurrentUser\My' |
        Where-Object {
            $_.Subject -eq $publisher -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(30)
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

if ($null -eq $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $publisher `
        -FriendlyName 'OpenTrace MSIX local test certificate' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears($ValidityYears) `
        -TextExtension @(
            '2.5.29.19={text}',
            '2.5.29.37={text}1.3.6.1.5.5.7.3.3'
        )
}

$randomBytes = [byte[]]::new(48)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($randomBytes)
$plainPassword = [Convert]::ToBase64String($randomBytes)
$securePassword = ConvertTo-SecureString -String $plainPassword -AsPlainText -Force

try {
    Export-PfxCertificate `
        -Cert $certificate `
        -FilePath $pfxPath `
        -Password $securePassword `
        -ChainOption EndEntityCertOnly `
        -NoProperties `
        -Force | Out-Null

    Export-Certificate `
        -Cert $certificate `
        -FilePath $cerPath `
        -Type CERT `
        -Force | Out-Null

    # Export-Clixml protects SecureString values with Windows DPAPI. Only the
    # current Windows user on this computer can decrypt this file.
    $securePassword | Export-Clixml -LiteralPath $passwordPath -Force
}
finally {
    [Array]::Clear($randomBytes, 0, $randomBytes.Length)
    $plainPassword = $null
}

foreach ($trustStore in @(
    'Cert:\CurrentUser\Root',
    'Cert:\CurrentUser\TrustedPeople'
)) {
    $trustedCertificate = Get-ChildItem -LiteralPath $trustStore |
        Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } |
        Select-Object -First 1

    if ($null -eq $trustedCertificate) {
        Import-Certificate `
            -FilePath $cerPath `
            -CertStoreLocation $trustStore | Out-Null
    }
}

[PSCustomObject]@{
    Subject = $certificate.Subject
    Thumbprint = $certificate.Thumbprint
    NotAfter = $certificate.NotAfter
    PfxPath = $pfxPath
    CerPath = $cerPath
    PasswordPath = $passwordPath
}
