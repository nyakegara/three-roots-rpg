$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sources = Join-Path $repo 'docs/art/animations/sources'
$atlases = Join-Path $repo 'content/art/animations'
$art = Join-Path $repo 'content/art'
$previews = Join-Path $repo 'docs/art/animations/previews'

dotnet build (Join-Path $PSScriptRoot 'SpriteSheetTool.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'SpriteSheetTool build failed.' }
$tool = Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'
$specs = @(
 @{ Name = 'militia'; Kind = 'unit'; Box = '8,8,48,64' },
 @{ Name = 'longbow'; Kind = 'unit'; Box = '8,8,48,64' },
 @{ Name = 'banner-knight'; Kind = 'unit'; Box = '0,4,64,68' },
 @{ Name = 'order-hunter'; Kind = 'unit'; Box = '1,0,62,72' },
 @{ Name = 'inquisitor'; Kind = 'support'; Box = '1,0,62,72' }
)

Add-Type -AssemblyName System.Drawing
foreach ($spec in $specs) {
 $name = 'unit.human.' + $spec.Name + '.actions'
 $ability = if ($spec.Name -eq 'inquisitor') { 'holy-fire' } else { 'ability' }
 dotnet $tool --components --source (Join-Path $sources ('human-' + $spec.Name + '.source.png')) `
  --output (Join-Path $atlases ($name + '.png')) --kind $spec.Kind --id $name `
  --content-box $spec.Box --alpha-cutoff 128 --align-bottom 72 --ability-name $ability `
  --contact-strip (Join-Path $previews ($name + '.contact.png'))
 if ($LASTEXITCODE -ne 0) { throw "Export failed: $name" }

 $metadata = Get-Content (Join-Path $atlases ($name + '.json')) -Raw | ConvertFrom-Json
 if (@($metadata.frameDiagnostics | Where-Object { $_.empty -or $_.edgePixels -gt 0 }).Count -gt 0 -or
     @($metadata.unpacking.frames | Where-Object { $_.translatedClipping -gt 0 -or $_.sourcePixelsProjectedOutsideFrame -gt 0 }).Count -gt 0) {
  throw "Empty, edge-touching or clipped frame: $name"
 }
 $sheet = [Drawing.Bitmap]::new((Join-Path $atlases ($name + '.png')))
 try {
  $idle = $sheet.Clone([Drawing.Rectangle]::new(0, 0, 64, 80), $sheet.PixelFormat)
  try { $idle.Save((Join-Path $art ('unit.human.' + $spec.Name + '.png')), [Drawing.Imaging.ImageFormat]::Png) }
  finally { $idle.Dispose() }
 }
 finally { $sheet.Dispose() }
}

& (Join-Path $PSScriptRoot 'Build-Preview.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Preview build failed.' }
Write-Host 'PASS: five human action sheets, static sprites and preview exported.'
