$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sources = Join-Path $repo 'docs/art/animations/sources'
$atlases = Join-Path $repo 'content/art/animations'
$previews = Join-Path $repo 'docs/art/animations/previews'
$specs = @(
 @{ Name = 'flora.thorn-guard'; Source = 'flora-thorn-guard'; Box = '8,16,48,56' },
 @{ Name = 'human.militia'; Source = 'human-militia'; Box = '8,8,48,64' },
 @{ Name = 'human.banner-knight'; Source = 'human-banner-knight'; Box = '1,4,62,68' },
 @{ Name = 'human.inquisitor'; Source = 'human-inquisitor'; Box = '0,4,64,68' }
)

dotnet build (Join-Path $PSScriptRoot 'SpriteSheetTool.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'SpriteSheetTool build failed.' }
$tool = Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'
Add-Type -AssemblyName System.Drawing
foreach ($spec in $specs) {
 $id = 'unit.' + $spec.Name + '.idle'
 dotnet $tool --components --source (Join-Path $sources ($spec.Source + '.idle.source.png')) `
  --output (Join-Path $atlases ($id + '.png')) --kind idle --id $id --anchor-mode body `
  --content-box $spec.Box --alpha-cutoff 128 --align-bottom 72 --frame-ms 260 `
  --contact-strip (Join-Path $previews ($id + '.contact.png'))
 if ($LASTEXITCODE -ne 0) { throw "Export failed: $id" }
 $meta = Get-Content (Join-Path $atlases ($id + '.json')) -Raw | ConvertFrom-Json
 if ($meta.clips.Count -ne 1 -or $meta.clips[0].name -ne 'idle' -or !$meta.clips[0].loop -or
     @($meta.frameDiagnostics | Where-Object { $_.empty -or $_.edgePixels -gt 0 }).Count -gt 0 -or
     @($meta.unpacking.frames | Where-Object { $_.translatedClipping -gt 0 -or $_.sourcePixelsProjectedOutsideFrame -gt 0 }).Count -gt 0) {
  throw "Invalid or clipped idle clip: $id"
 }
 $sheet = [Drawing.Bitmap]::new((Join-Path $atlases ($id + '.png')))
 try {
  $footCenters = @()
  for ($frame = 0; $frame -lt 4; $frame++) {
   $feet = @()
   for ($y = 64; $y -lt 73; $y++) {
    for ($x = 0; $x -lt 64; $x++) {
     if ($sheet.GetPixel($frame * 64 + $x, $y).A -gt 127) { $feet += $x }
    }
   }
   if (!$feet.Count) { throw "Missing planted feet: $id frame $frame" }
   $footCenters += (($feet | Measure-Object -Minimum).Minimum + ($feet | Measure-Object -Maximum).Maximum) / 2
  }
  if (($footCenters | Measure-Object -Maximum).Maximum - ($footCenters | Measure-Object -Minimum).Minimum -gt 1) {
   throw "Idle foot anchor drifts: $id"
  }
 }
 finally { $sheet.Dispose() }
}
& (Join-Path $PSScriptRoot 'Build-Preview.ps1')
Write-Host 'PASS: four stable idle sheets exported.'
