param([switch]$HeroOnly)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$documentation = Join-Path $repository 'docs/art/animations'
$sourceDirectory = Join-Path $documentation 'sources'
$outputDirectory = Join-Path $repository 'content/art/animations'
$previewDirectory = Join-Path $documentation 'previews'
$requiredSources = @('hero.base.source.png','hero.rotation4.source.png','hero.se.source.png','hero.walk.north.source.png','hero.walk.se.source.png','hero.walk.south.source.png','thorn-guard.source.png','sprout.source.png','sprout.heal.source.png','ember-bloom.source.png','pod-slinger.source.png','root-warden.source.png')
foreach ($name in $requiredSources) {
 if (!(Test-Path -LiteralPath (Join-Path $sourceDirectory $name))) { throw "Missing selected source: $name" }
}
if (@(Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.png' -File).Count -ne 12) { throw 'Source archive must contain exactly twelve selected/historical PNGs.' }
$sourcesBefore = @{}
foreach ($name in $requiredSources) { $sourcesBefore[$name] = (Get-FileHash -LiteralPath (Join-Path $sourceDirectory $name) -Algorithm SHA256).Hash }
$specifications = @(
 @{Name='hero.flora.walk';Source='hero.base.source.png';Kind='hero';Box='0,8,64,64';RowMap='hero.rows.json';Ability='ability';AnchorMode='cell';HeroTwoFrame=$true},
 @{Name='unit.flora.thorn-guard.actions';Source='thorn-guard.source.png';Kind='unit';Box='8,16,48,56';Ability='ability'},
 @{Name='unit.flora.sprout.actions';Source='sprout.source.png';Kind='support';Box='8,8,48,64';RowMap='sprout.rows.json';Ability='heal'},
 @{Name='unit.flora.ember-bloom.actions';Source='ember-bloom.source.png';Kind='unit';Box='8,8,48,64';Ability='ability'},
 @{Name='unit.flora.pod-slinger.actions';Source='pod-slinger.source.png';Kind='unit';Box='8,8,48,64';Ability='ability'},
 @{Name='unit.flora.root-warden.actions';Source='root-warden.source.png';Kind='support';Box='8,8,48,64';Ability='conversion-hit'}
)
$heroMap=Get-Content -LiteralPath (Join-Path $documentation 'hero.rows.json') -Raw|ConvertFrom-Json
$se=@($heroMap|Where-Object {$_.targetRow -eq 3});$idle=@($heroMap|Where-Object {$_.targetRow -eq 8})
if($se.Count -ne 1 -or $idle.Count -ne 1 -or $se[0].source -ne $idle[0].source -or $se[0].sourceRow -ne 0 -or $idle[0].sourceRow -ne 0 -or $se[0].sourceScale -ne $idle[0].sourceScale){throw 'Install the approved two-frame hero row map: idle row8 must reuse SE walk source row0 and the same sourceScale.'}
$unitHashesBefore=@{}
if($HeroOnly){foreach($specification in $specifications|Where-Object {$_.Kind -ne 'hero'}){foreach($extension in @('.png','.json')){$path=Join-Path $outputDirectory ($specification.Name+$extension);$unitHashesBefore[$path]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash};$path=Join-Path $previewDirectory ($specification.Name+'.contact.png');$unitHashesBefore[$path]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}}
dotnet build (Join-Path $PSScriptRoot 'SpriteSheetTool.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'SpriteSheetTool build failed.' }
$tool = Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'
foreach ($specification in $specifications) {
 if($HeroOnly -and $specification.Kind -ne 'hero'){continue}
 $arguments = @('--components','--source',(Join-Path $sourceDirectory $specification.Source),'--output',(Join-Path $outputDirectory ($specification.Name+'.png')),'--kind',$specification.Kind,'--id',$specification.Name,'--content-box',$specification.Box,'--alpha-cutoff','128','--align-bottom','72','--ability-name',$specification.Ability)
 if($specification.AnchorMode){$arguments+=@('--anchor-mode',$specification.AnchorMode)}
 if($specification.HeroTwoFrame){$arguments+='--hero-two-frame'}
 $arguments+=@('--contact-strip',(Join-Path $previewDirectory ($specification.Name+'.contact.png')))
 if ($specification.RowMap) { $arguments += @('--row-map',(Join-Path $documentation $specification.RowMap)) }
 & dotnet $tool @arguments
 if ($LASTEXITCODE -ne 0) { throw "Export failed: $($specification.Name)" }
}
if(!$HeroOnly){@{sheets=@($specifications | ForEach-Object { '../../../content/art/animations/'+$_.Name+'.json' })} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $documentation 'manifest.json') -Encoding utf8}

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
  $mode=if($metadata.anchorMode){$metadata.anchorMode}else{'body'}
  $expectedMode=if($specification.Kind -eq 'hero'){'cell'}else{'body'}
  if($mode -ne $expectedMode){throw "Wrong anchor mode: $($specification.Name)"}
  if (!$metadata.unpacking.enabled -or $metadata.cell.width -ne 64 -or $metadata.cell.height -ne 80 -or $metadata.anchor.x -ne 32 -or $metadata.anchor.y -ne 72) { throw 'Wrong unpacking/frame contract.' }
  if($specification.Kind -eq 'hero'){
   if(!$metadata.heroTwoFrame -or $metadata.columns -ne 2 -or $metadata.rows -ne 9 -or $metadata.width -ne 128 -or $metadata.height -ne 720 -or $metadata.unpacking.frames.Count -ne 18 -or $metadata.usedFrameCount -ne 17 -or $metadata.clips.Count -ne 9){throw 'Wrong two-frame hero contract.'}
   $used=@();for($row=0;$row -lt 8;$row++){$clip=$metadata.clips[$row];if($clip.frames.Count -ne 2 -or $clip.frames[0] -ne $row*2 -or $clip.frames[1] -ne $row*2+1 -or $clip.frameMs -ne 220 -or !$clip.loop){throw 'Wrong two-frame walk clip.'};$used+=$clip.frames}
   $idleClip=$metadata.clips[8];if($idleClip.frames.Count -ne 1 -or $idleClip.frames[0] -ne 16 -or $idleClip.loop){throw 'Idle must use only frame16 without looping.'};$used+=$idleClip.frames
   if(@($used|Select-Object -Unique).Count -ne 17 -or $used -contains 17){throw 'Wrong used hero slots.'}
  }
  $null=Resolve-RepositoryPath $outputDirectory $metadata.source.image
  $sourceInfo=@{};$rowGrounds=@{};$rowOffsets=@{}
  foreach ($source in $metadata.unpacking.sources) {
   $sourceInfo[$source.source]=$source
   if ($source.primaryComponents -ne $source.expected) { throw 'Primary-component count mismatch.' }
   $sourcePath=Resolve-RepositoryPath $outputDirectory $source.source
   if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne $source.sha256) { throw 'Source provenance hash mismatch.' }
   if (@($source.Attachments|Where-Object {$_.far -or $_.ambiguous}).Count) { throw 'Unresolved detached effect.' }
  }
  if($mode -eq 'cell'){foreach($frame in $metadata.unpacking.frames){$info=$sourceInfo[$frame.source];$sourceRow=[int][Math]::Floor($frame.sourceFrame/$info.Columns);$key=$frame.source+'#'+$sourceRow;if($info.RowGrounds){$rowGrounds[$key]=$info.RowGrounds[$sourceRow]}else{if(!$rowGrounds.ContainsKey($key)){$rowGrounds[$key]=0};$rowGrounds[$key]=[Math]::Max($rowGrounds[$key],$frame.primaryBounds.bottom)}}}
  $image=[System.Drawing.Bitmap]::new((Join-Path $outputDirectory $metadata.image))
  try {
   foreach ($frame in $metadata.unpacking.frames) {
    $frames++
    if ($frame.translatedClipping -or $frame.sourcePixelsProjectedOutsideFrame) { throw 'Reported clipping.' }
    $sourcePath=Resolve-RepositoryPath $outputDirectory $frame.source
    if (!$sourceImages.ContainsKey($sourcePath)) { $sourceImages[$sourcePath]=[System.Drawing.Bitmap]::new($sourcePath) }
    if ($scales.ContainsKey($sourcePath) -and $scales[$sourcePath] -ne $frame.scale) { throw 'Nonconstant source scale.' }
    $scales[$sourcePath]=$frame.scale;$sourceImage=$sourceImages[$sourcePath];$bottom=0
    $registrationY=[double]$metadata.registration[$frame.frame].translationY
    $pivotY=if($null -ne $frame.pivotY){[double]$frame.pivotY}else{[double]$frame.primaryBottom}
    if($specification.Kind -eq 'hero'){
     $destinationRow=[int][Math]::Floor($frame.frame/2);$expectedColumn=if($destinationRow -eq 8 -or $frame.frame%2 -eq 0){0}else{3};$info=$sourceInfo[$frame.source]
     if($frame.sourceColumn -ne $expectedColumn -or $frame.sourceFrame%$info.Columns -ne $expectedColumn){throw 'Hero sampled the wrong source column.'}
     if($destinationRow -eq 8){$seFrame=$metadata.unpacking.frames[6];if($frame.source -ne $seFrame.source -or $frame.sourceFrame -ne $seFrame.sourceFrame -or $frame.scale -ne $seFrame.scale){throw 'Idle provenance differs from SE walk column0.'}}
    }
    if($mode -eq 'cell'){
     $info=$sourceInfo[$frame.source];$cw=[double]$info.Width/$info.Columns;$column=$frame.sourceFrame%$info.Columns;$key=$frame.source+'#'+[Math]::Floor($frame.sourceFrame/$info.Columns)
     if($registrationY -ne 0 -or [Math]::Abs([double]$frame.pivotX-($column+.5)*$cw) -gt 0.000001 -or $pivotY -ne $rowGrounds[$key]){throw 'Cell mode has a moving pivot or per-frame registration.'}
     $relativeX=[double]$frame.transform.offsetX+$column*$cw*[double]$frame.scale
     if(!$rowOffsets.ContainsKey($key)){$rowOffsets[$key]=@($relativeX,[double]$frame.transform.offsetY)}elseif([Math]::Abs($relativeX-$rowOffsets[$key][0]) -gt 0.000001 -or [Math]::Abs([double]$frame.transform.offsetY-$rowOffsets[$key][1]) -gt 0.000001){throw 'Cell-relative offsets vary inside a source row.'}
    }
    for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){
     $pixel=$image.GetPixel(($frame.frame%$metadata.columns)*64+$x,[Math]::Floor($frame.frame/$metadata.columns)*80+$y)
     if($pixel.A -ne 0 -and $pixel.A -ne 255){throw 'Nonbinary native alpha.'}
     if($pixel.A -eq 0){continue};$opaquePixels++;$bottom=[Math]::Max($bottom,$y+1)
     if($x -lt $metadata.contentBox.x -or $x -ge $metadata.contentBox.x+$metadata.contentBox.width -or $y -lt $metadata.contentBox.y -or $y -ge 72){throw 'Pixel outside fixed content box.'}
     $sx=[int][Math]::Floor([double]$frame.pivotX+($x+.5-[double]$metadata.anchor.x)/[double]$frame.scale)
     $sy=[int][Math]::Floor($pivotY+($y-$registrationY+.5-[double]$metadata.anchor.y)/[double]$frame.scale)
     $original=$sourceImage.GetPixel($sx,$sy)
     if($original.A -lt 128 -or $pixel.R -ne $original.R -or $pixel.G -ne $original.G -or $pixel.B -ne $original.B){throw 'Sampled RGB/alpha differs from source.'}
    }}
    if($mode -eq 'body' -and $bottom -ne 72){throw "Wrong visible body baseline: $($specification.Name), frame$($frame.frame)"}
    if($bottom -lt 1 -or $bottom -gt 72 -or $bottom -ne $metadata.registration[$frame.frame].registeredBottomExclusive){throw "Wrong registered bottom: $($specification.Name), frame$($frame.frame)"}
   }
   if($specification.Kind -eq 'hero'){for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){$idlePixel=$image.GetPixel($x,640+$y).ToArgb();if($idlePixel -ne $image.GetPixel(64+$x,640+$y).ToArgb() -or $idlePixel -ne $image.GetPixel($x,240+$y).ToArgb()){throw 'Idle is not a static duplicate of SE column0.'}}}}
  }finally{$image.Dispose()}
 }
}finally{foreach($image in $sourceImages.Values){$image.Dispose()}}
foreach($name in $requiredSources){if((Get-FileHash -LiteralPath (Join-Path $sourceDirectory $name) -Algorithm SHA256).Hash -ne $sourcesBefore[$name]){throw "Source archive was modified: $name"}}
foreach($path in $unitHashesBefore.Keys){if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $unitHashesBefore[$path]){throw "HeroOnly modified a unit file: $path"}}
if($frames -ne 74){throw "Expected74 total slots, found$frames"}
"PASS: 6 portable atlases verified; 12 unchanged archived sources; $frames total slots (hero18/17used); $opaquePixels native opaque RGB samples; binary alpha; body/cell ground contract; constant source scales; no clipping; HeroOnly=$HeroOnly; unit hashes preserved=$($unitHashesBefore.Count)."
