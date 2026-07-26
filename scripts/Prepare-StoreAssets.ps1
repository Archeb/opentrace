[CmdletBinding()]
param(
    [string]$SourcePath = 'HomePage\img\logo.png',

    [string]$OutputDirectory = 'artifacts\store-assets'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

if (![System.IO.Path]::IsPathRooted($SourcePath)) {
    $SourcePath = Join-Path $repositoryRoot $SourcePath
}
$SourcePath = [System.IO.Path]::GetFullPath($SourcePath)

if (![System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

if (!(Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
    throw "Store logo source was not found: $SourcePath"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Add-Type -AssemblyName System.Drawing

$assets = @(
    @{ Name = 'Square44x44Logo.png'; Size = 44 },
    @{ Name = 'Square150x150Logo.png'; Size = 150 },
    @{ Name = 'StoreLogo.png'; Size = 50 }
)

$sourceImage = [System.Drawing.Image]::FromFile($SourcePath)
try {
    foreach ($asset in $assets) {
        $outputPath = Join-Path $OutputDirectory $asset.Name
        $bitmap = New-Object System.Drawing.Bitmap $asset.Size, $asset.Size
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.DrawImage($sourceImage, 0, 0, $asset.Size, $asset.Size)
            }
            finally {
                $graphics.Dispose()
            }

            $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $sourceImage.Dispose()
}

Write-Output $OutputDirectory
