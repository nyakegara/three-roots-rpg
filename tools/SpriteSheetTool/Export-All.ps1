$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$documentation = Join-Path $repository 'docs/art/animations'
$sourceDirectory = Join-Path $documentation 'sources'
$outputDirectory = Join-Path $repository 'content/art/animations'
$previewDirectory = Join-Path $documentation 'previews'
$requiredSources = @('hero.base.source.png','hero.rotation4.source.png','hero.se.source.png','thorn-guard.source.png','sprout.source.png','sprout.heal.source.png','ember-bloom.source.png','pod-slinger.source.png','root-warden.source.png')
foreach ($name in $requiredSources) {
 if (!(Test-Path -LiteralPath (Join-Path $sourceDirectory $name))) { throw "Missing selected source: $name" }
}
if (@(Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.png' -File).Count -ne 9) { throw 'Source archive must contain exactly the nine selected PNGs.' }
$sourcesBefore = @{}
foreach ($name in $requiredSources) { $sourcesBefore[$name] = (Get-FileHash -LiteralPath (Join-Path $sourceDirectory $name) -Algorithm SHA256).Hash }
$specifications = @(
 @{Name='hero.flora.walk';Source='hero.base.source.png';Kind='hero';Box='8,8,48,64';RowMap='hero.rows.json';Ability='ability'},
 @{Name='unit.flora.thorn-guard.actions';Source='thorn-guard.source.png';Kind='unit';Box='8,16,48,56';Ability='ability'},
 @{Name='unit.flora.sprout.actions';Source='sprout.source.png';Kind='support';Box='8,8,48,64';RowMap='sprout.rows.json';Ability='heal'},
 @{Name='unit.flora.ember-bloom.actions';Source='ember-bloom.source.png';Kind='unit';Box='8,8,48,64';Ability='ability'},
 @{Name='unit.flora.pod-slinger.actions';Source='pod-slinger.source.png';Kind='unit';Box='8,8,48,64';Ability='ability'},
 @{Name='unit.flora.root-warden.actions';Source='root-warden.source.png';Kind='support';Box='8,8,48,64';Ability='conversion-hit'}
)
dotnet build (Join-Path $PSScriptRoot 'SpriteSheetTool.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'SpriteSheetTool build failed.' }
$tool = Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'
foreach ($specification in $specifications) {
 $arguments = @('--components','--source',(Join-Path $sourceDirectory $specification.Source),'--output',(Join-Path $outputDirectory ($specification.Name+'.png')),'--kind',$specification.Kind,'--id',$specification.Name,'--content-box',$specification.Box,'--alpha-cutoff','128','--align-bottom','72','--ability-name',$specification.Ability,'--contact-strip',(Join-Path $previewDirectory ($specification.Name+'.contact.png')))
 if ($specification.RowMap) { $arguments += @('--row-map',(Join-Path $documentation $specification.RowMap)) }
 & dotnet $tool @arguments
 if ($LASTEXITCODE -ne 0) { throw "Export failed: $($specification.Name)" }
}
@{sheets=@($specifications | ForEach-Object { '../../../content/art/animations/'+$_.Name+'.json' })} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $documentation 'manifest.json') -Encoding utf8

# Verify actual exported pixels rather than trusting metadata alone.
Add-Type -AssemblyName System.Drawing
$sourceImages=@{};$frames=0;$opaquePixels=0;$scales=@{}
function Resolve-RepositoryPath([string]$Base,[string]$Relative) {
 if ([IO.Path]::IsPathRooted($Relative)) { throw "Nonportable absolute image path: $Relative" }
 $path=[IO.Path]::GetFullPath((Join-Path $Base $Relative))
 if (!$path.StartsWith($repository+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Image path escapes repository: $Relative" }
 if (!(Test-Path -LiteralPath $path)) { throw "Missing referenced image: $Relative" }
 return $path
}
try {
 foreach ($specification in $specifications) {
  $metadataPath=Join-Path $outputDirectory ($specification.Name+'.json');$raw=Get-Content -LiteralPath $metadataPath -Raw
  if ($raw -match '(?i)(\.codex|artifacts[\\/]|[a-z]:[\\/])') { throw "Machine/review path in $metadataPath" }
  $metadata=$raw|ConvertFrom-Json
  if (!$metadata.unpacking.enabled -or $metadata.cell.width -ne 64 -or $metadata.cell.height -ne 80 -or $metadata.anchor.x -ne 32 -or $metadata.anchor.y -ne 72) { throw 'Wrong unpacking/frame contract.' }
  $null=Resolve-RepositoryPath $outputDirectory $metadata.source.image
  foreach ($source in $metadata.unpacking.sources) {
   if ($source.primaryComponents -ne $source.expected) { throw 'Primary-component count mismatch.' }
   $sourcePath=Resolve-RepositoryPath $outputDirectory $source.source
   if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $source.sha256) { throw 'Source provenance hash mismatch.' }
   if (@($source.Attachments|Where-Object {$_.far -or $_.ambiguous}).Count) { throw 'Unresolved detached effect.' }
  }
  $image=[System.Drawing.Bitmap]::new((Join-Path $outputDirectory $metadata.image))
  try {
   foreach ($frame in $metadata.unpacking.frames) {
    $frames++
    if ($frame.translatedClipping -or $frame.sourcePixelsProjectedOutsideFrame) { throw 'Reported clipping.' }
    $sourcePath=Resolve-RepositoryPath $outputDirectory $frame.source
    if (!$sourceImages.ContainsKey($sourcePath)) { $sourceImages[$sourcePath]=[System.Drawing.Bitmap]::new($sourcePath) }
    if ($scales.ContainsKey($sourcePath) -and $scales[$sourcePath] -ne $frame.scale) { throw 'Nonconstant source scale.' }
    $scales[$sourcePath]=$frame.scale;$sourceImage=$sourceImages[$sourcePath];$bottom=0
    for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){
     $pixel=$image.GetPixel(($frame.frame%$metadata.columns)*64+$x,[Math]::Floor($frame.frame/$metadata.columns)*80+$y)
     if($pixel.A -ne 0 -and $pixel.A -ne 255){throw 'Nonbinary native alpha.'}
     if($pixel.A -eq 0){continue};$opaquePixels++;$bottom=[Math]::Max($bottom,$y+1)
     if($x -lt $metadata.contentBox.x -or $x -ge $metadata.contentBox.x+$metadata.contentBox.width -or $y -lt $metadata.contentBox.y -or $y -ge 72){throw 'Pixel outside fixed content box.'}
     $sx=[int][Math]::Floor(($x+.5-[double]$frame.transform.offsetX)/[double]$frame.scale)
     $sy=[int][Math]::Floor(($y+.5-[double]$frame.transform.offsetY)/[double]$frame.scale)
     $original=$sourceImage.GetPixel($sx,$sy)
     if($original.A -lt 128 -or $pixel.R -ne $original.R -or $pixel.G -ne $original.G -or $pixel.B -ne $original.B){throw 'Sampled RGB/alpha differs from source.'}
    }}
    if($bottom -ne 72){throw "Wrong visible baseline: $($specification.Name), frame$($frame.frame)"}
   }
  }finally{$image.Dispose()}
 }
}finally{foreach($image in $sourceImages.Values){$image.Dispose()}}
foreach($name in $requiredSources){if((Get-FileHash -LiteralPath (Join-Path $sourceDirectory $name) -Algorithm SHA256).Hash -ne $sourcesBefore[$name]){throw "Source archive was modified: $name"}}
"PASS: 6 portable atlases; 9 unchanged archived sources; $frames frames; $opaquePixels native opaque RGB samples; binary alpha; ground72; constant source scales; no clipping."
