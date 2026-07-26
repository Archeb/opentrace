[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.5.1.0',

    [string]$OutputDirectory = 'artifacts\store-signed',

    [string]$CertificateOutputDirectory = 'artifacts\store-test-certificate',

    [switch]$RenewCertificate
)

$ErrorActionPreference = 'Stop'

$certificate = & (Join-Path $PSScriptRoot 'New-StoreTestCertificate.ps1') `
    -OutputDirectory $CertificateOutputDirectory `
    -Force:$RenewCertificate

$certificatePassword = Import-Clixml -LiteralPath $certificate.PasswordPath
if ($certificatePassword -isnot [SecureString]) {
    throw "The certificate password file is invalid: $($certificate.PasswordPath)"
}

& (Join-Path $PSScriptRoot 'Build-StorePackage.ps1') `
    -Version $Version `
    -OutputDirectory $OutputDirectory `
    -CertificatePath $certificate.PfxPath `
    -CertificatePassword $certificatePassword

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    $outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

$bundlePath = Join-Path $outputRoot "OpenTrace_$($Version)_x64_arm64.msixbundle"

Write-Host ''
Write-Host "Signed local-test bundle: $bundlePath"
Write-Host "Public test certificate: $($certificate.CerPath)"
Write-Host "Certificate thumbprint: $($certificate.Thumbprint)"
Write-Host 'The PFX password is protected with Windows DPAPI for the current user.'

[PSCustomObject]@{
    BundlePath = $bundlePath
    CertificatePath = $certificate.CerPath
    CertificateThumbprint = $certificate.Thumbprint
    CertificateExpires = $certificate.NotAfter
}
