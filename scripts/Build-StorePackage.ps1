[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.5.1.0',

    [string]$OutputDirectory = 'artifacts\store',

    [string]$CertificatePath,

    [SecureString]$CertificatePassword,

    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'OpenTrace.csproj'
$manifestTemplate = Join-Path $repositoryRoot 'OpenTrace.Package\Package.appxmanifest'
$prepareStoreAssets = Join-Path $PSScriptRoot 'Prepare-StoreAssets.ps1'

if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    $outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

function Reset-ChildDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $outputPrefix = $outputRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (!$fullPath.StartsWith($outputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset a directory outside the package output root: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Get-WindowsSdkTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $candidates = Get-ChildItem -LiteralPath $kitsRoot -Recurse -Filter $Name -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'x64' -and $_.Directory.Parent.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [Version]$_.Directory.Parent.Name } -Descending

    $tool = $candidates | Select-Object -First 1
    if ($null -eq $tool) {
        throw "$Name was not found. Install the Windows 10/11 SDK."
    }

    return $tool.FullName
}

function Assert-LastExitCode {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Operation
    )

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE."
    }
}

function Get-VerifiedFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,

        [Parameter(Mandatory = $true)]
        [string]$Destination,

        [Parameter(Mandatory = $true)]
        [string]$Sha256
    )

    $destinationDirectory = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

    if (Test-Path -LiteralPath $Destination) {
        $existingHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
        if ($existingHash.Equals($Sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
        Remove-Item -LiteralPath $Destination -Force
    }

    Invoke-WebRequest -Uri $Uri -OutFile $Destination -Headers @{ 'User-Agent' = 'OpenTrace-StoreBuild' }
    $actualHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if (!$actualHash.Equals($Sha256, [System.StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $Destination -Force
        throw "SHA256 mismatch for $Uri. Expected $Sha256, received $actualHash."
    }
}

function Set-PackageManifestIdentity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Architecture
    )

    [xml]$manifest = Get-Content -LiteralPath $manifestTemplate -Raw
    $namespace = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $namespace.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $identity = $manifest.SelectSingleNode('/f:Package/f:Identity', $namespace)
    $identity.SetAttribute('Version', $Version)
    $identity.SetAttribute('ProcessorArchitecture', $Architecture)

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $manifest.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

$makeAppx = Get-WindowsSdkTool -Name 'makeappx.exe'
$signTool = $null
if ($CertificatePath) {
    if (!(Test-Path -LiteralPath $CertificatePath)) {
        throw "Certificate not found: $CertificatePath"
    }
    $signTool = Get-WindowsSdkTool -Name 'signtool.exe'
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$workRoot = Join-Path $outputRoot 'work'
$cacheRoot = Join-Path $outputRoot 'cache\v1.7.1'
$packagesRoot = Join-Path $outputRoot 'packages'
$bundleInputRoot = Join-Path $workRoot 'bundle-input'
$packageImages = Join-Path $workRoot 'package-images'

Reset-ChildDirectory -Path $workRoot
Reset-ChildDirectory -Path $packagesRoot
& $prepareStoreAssets `
    -SourcePath (Join-Path $repositoryRoot 'HomePage\img\logo.png') `
    -OutputDirectory $packageImages | Out-Null

$architectures = @(
    @{
        Name = 'x64'
        RuntimeIdentifier = 'win-x64'
        Framework = 'net48'
        NextTraceUri = 'https://github.com/nxtrace/NTrace-core/releases/download/v1.7.1/nexttrace_windows_amd64.exe'
        NextTraceSha256 = '2aa0e4c4540430cab46544b8a1bf93d20291550e93529d04b31cf0fd6197b057'
    },
    @{
        Name = 'arm64'
        RuntimeIdentifier = 'win-arm64'
        Framework = 'net481'
        NextTraceUri = 'https://github.com/nxtrace/NTrace-core/releases/download/v1.7.1/nexttrace_windows_arm64.exe'
        NextTraceSha256 = 'cce668dbb0d8c2dbe2d1f5b255f8f9e7e36cc6feecc785db06b6a42b3f37f2f0'
    }
)

$builtPackages = @()
$applicationVersion = ($Version.Split('.')[0..2] -join '.')

foreach ($architecture in $architectures) {
    $architectureName = $architecture.Name
    $runtimeIdentifier = $architecture.RuntimeIdentifier
    $layout = Join-Path $workRoot "$architectureName\layout"
    Reset-ChildDirectory -Path $layout

    Write-Host "Building OpenTrace $architectureName..."
    & dotnet restore $projectPath "/p:RuntimeIdentifier=$runtimeIdentifier"
    Assert-LastExitCode -Operation "dotnet restore ($architectureName)"

    & dotnet build $projectPath `
        --configuration Release `
        --no-restore `
        --output $layout `
        "/p:RuntimeIdentifier=$runtimeIdentifier" `
        "/p:Version=$applicationVersion"
    Assert-LastExitCode -Operation "dotnet build ($architectureName)"

    Get-ChildItem -LiteralPath $layout -Filter '*.pdb' -File -ErrorAction SilentlyContinue |
        Remove-Item -Force

    $nextTraceCache = Join-Path $cacheRoot "$architectureName\nexttrace.exe"
    Get-VerifiedFile `
        -Uri $architecture.NextTraceUri `
        -Destination $nextTraceCache `
        -Sha256 $architecture.NextTraceSha256
    Copy-Item -LiteralPath $nextTraceCache -Destination (Join-Path $layout 'nexttrace.exe') -Force

    if ($architectureName -eq 'x64') {
        $winDivertFiles = @(
            @{
                Name = 'WinDivert.dll'
                Uri = 'https://raw.githubusercontent.com/nxtrace/NTrace-core/v1.7.1/assets/windivert/x64/WinDivert.dll'
                Sha256 = 'c1e060ee19444a259b2162f8af0f3fe8c4428a1c6f694dce20de194ac8d7d9a2'
            },
            @{
                Name = 'WinDivert64.sys'
                Uri = 'https://raw.githubusercontent.com/nxtrace/NTrace-core/v1.7.1/assets/windivert/x64/WinDivert64.sys'
                Sha256 = '8da085332782708d8767bcace5327a6ec7283c17cfb85e40b03cd2323a90ddc2'
            }
        )

        foreach ($winDivertFile in $winDivertFiles) {
            $cachePath = Join-Path $cacheRoot "x64\$($winDivertFile.Name)"
            Get-VerifiedFile `
                -Uri $winDivertFile.Uri `
                -Destination $cachePath `
                -Sha256 $winDivertFile.Sha256
            Copy-Item -LiteralPath $cachePath -Destination (Join-Path $layout $winDivertFile.Name) -Force
        }
    }

    Copy-Item -LiteralPath $packageImages -Destination (Join-Path $layout 'Images') -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE.txt') -Destination $layout -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') -Destination $layout -Force
    Set-PackageManifestIdentity -Path (Join-Path $layout 'AppxManifest.xml') -Architecture $architectureName

    $packagePath = Join-Path $packagesRoot "OpenTrace_$($Version)_$architectureName.msix"
    & $makeAppx pack /d $layout /p $packagePath /o
    Assert-LastExitCode -Operation "MakeAppx pack ($architectureName)"
    $builtPackages += $packagePath
}

Reset-ChildDirectory -Path $bundleInputRoot
foreach ($packagePath in $builtPackages) {
    Copy-Item -LiteralPath $packagePath -Destination $bundleInputRoot -Force
}

$bundlePath = Join-Path $outputRoot "OpenTrace_$($Version)_x64_arm64.msixbundle"
if (Test-Path -LiteralPath $bundlePath) {
    Remove-Item -LiteralPath $bundlePath -Force
}
& $makeAppx bundle /d $bundleInputRoot /p $bundlePath /o
Assert-LastExitCode -Operation 'MakeAppx bundle'

if ($CertificatePath) {
    $passwordPointer = [IntPtr]::Zero
    try {
        if ($null -ne $CertificatePassword) {
            $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($CertificatePassword)
            $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringUni($passwordPointer)
            & $signTool sign /fd SHA256 /a /f $CertificatePath /p $plainPassword $bundlePath
        }
        else {
            & $signTool sign /fd SHA256 /a /f $CertificatePath $bundlePath
        }
        Assert-LastExitCode -Operation 'SignTool'

        & $signTool verify /pa /v $bundlePath
        Assert-LastExitCode -Operation 'SignTool verification'
    }
    finally {
        if ($passwordPointer -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($passwordPointer)
        }
        $plainPassword = $null
    }
}

if (!$KeepStaging) {
    Remove-Item -LiteralPath $workRoot -Recurse -Force
}

Write-Host ''
Write-Host "Store bundle: $bundlePath"
Write-Host 'The bundle is unsigned unless -CertificatePath was supplied.'
Write-Output $bundlePath
