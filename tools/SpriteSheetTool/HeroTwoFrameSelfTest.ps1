param([string]$ToolDll=(Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'))
$ErrorActionPreference='Stop';$PSNativeCommandUseErrorActionPreference=$false
Add-Type -AssemblyName System.Drawing
$directory=Join-Path ([IO.Path]::GetTempPath()) ('sprite-hero-two-frame-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $directory|Out-Null
$source=Join-Path $directory 'six-column-atlas.png';$bitmap=[System.Drawing.Bitmap]::new(240,720)
for($row=0;$row -lt 9;$row++){for($column=0;$column -lt 6;$column++){
 $color=[System.Drawing.Color]::FromArgb(255,10+$column*30,20+$row*20,80)
 $bottom=if($column -eq 5){70}else{60}
 for($y=10;$y -lt $bottom;$y++){for($x=10;$x -lt 30;$x++){$bitmap.SetPixel($column*40+$x,$row*80+$y,$color)}}
}}
$bitmap.Save($source,[System.Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose();$hash=(Get-FileHash $source).Hash
$map=Join-Path $directory 'rows.json';@(@{source=$source;sourceRow=3;targetRow=8;sourceColumns=6;sourceRows=9})|ConvertTo-Json -AsArray|Set-Content $map
$output=Join-Path $directory 'hero.png'
$log=& dotnet $ToolDll --components --hero-two-frame --anchor-mode cell --source $source --output $output --kind hero --id two.fixture --row-map $map --content-box 0,8,64,64 --alpha-cutoff 128 --align-bottom 72 2>&1
if($LASTEXITCODE -ne 0){throw "Two-frame export failed: $log"}
$meta=Get-Content ([IO.Path]::ChangeExtension($output,'.json')) -Raw|ConvertFrom-Json
if($meta.width -ne 128 -or $meta.height -ne 720 -or $meta.columns -ne 2 -or $meta.rows -ne 9 -or $meta.unpacking.frames.Count -ne 18){throw 'Expected128x720,2x9,18slots'}
if($meta.clips.Count -ne 9){throw 'Expected8walkclips+1idle'}
$used=@();for($row=0;$row -lt 8;$row++){$clip=$meta.clips[$row];if($clip.frames.Count -ne 2 -or $clip.frames[0] -ne $row*2 -or $clip.frames[1] -ne $row*2+1 -or $clip.frameMs -ne 220 -or !$clip.loop){throw 'Invalid two-frame walk clip'};$used+=$clip.frames}
$idle=$meta.clips[8];if($idle.frames.Count -ne 1 -or $idle.frames[0] -ne 16 -or $idle.loop){throw 'Idle must be one nonlooping frame16'};$used+=$idle.frames
if(@($used|Select-Object -Unique).Count -ne 17 -or $used -contains 17){throw 'Expected17usedslots and unusedslot17'}
$native=[System.Drawing.Bitmap]::new($output)
try{
 for($frame=0;$frame -lt 18;$frame++){
  $row=[int][Math]::Floor($frame/2);$sourceRow=if($row -eq 8){3}else{$row};$sourceColumn=if($row -eq 8 -or $frame%2 -eq 0){0}else{3};$record=$meta.unpacking.frames[$frame]
  if($record.sourceFrame -ne $sourceRow*6+$sourceColumn -or $record.sourceColumn -ne $sourceColumn){throw "Incorrect source provenance at slot$frame"}
  if($record.pivotY -ne $sourceRow*80+70){throw 'Row ground ignored unselected source column5'}
  $seen=0
  for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){$pixel=$native.GetPixel(($frame%2)*64+$x,$row*80+$y);if(!$pixel.A){continue};$seen++;if($pixel.A -ne 255 -or $pixel.R -ne 10+$sourceColumn*30 -or $pixel.G -ne 20+$sourceRow*20 -or $pixel.B -ne 80){throw "Wrong sampled frame/color at slot$frame"}}}
  if(!$seen){throw 'Empty selected frame'}
 }
 for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){
  $idle0=$native.GetPixel($x,640+$y).ToArgb();$idle1=$native.GetPixel(64+$x,640+$y).ToArgb();$se0=$native.GetPixel($x,240+$y).ToArgb()
  if($idle0 -ne $idle1 -or $idle0 -ne $se0){throw 'Idle slots are not exact copies of SE column0'}
 }}
}finally{$native.Dispose()}
if((Get-FileHash $source).Hash -ne $hash){throw 'Source changed'}
$invalid=Join-Path $directory 'invalid-unit.png';$log=& dotnet $ToolDll --components --hero-two-frame --source $source --output $invalid --kind unit --id invalid 2>&1
if($LASTEXITCODE -eq 0 -or (Test-Path $invalid) -or ($log -join ' ') -notmatch 'only valid.+hero'){throw 'Hero flag was not explicitly rejected for a unit'}
$invalidGrid=Join-Path $directory 'invalid-grid.png';$log=& dotnet $ToolDll --hero-two-frame --source $source --output $invalidGrid --kind hero --id invalid.grid 2>&1
if($LASTEXITCODE -eq 0 -or (Test-Path $invalidGrid) -or ($log -join ' ') -notmatch 'requires --components'){throw 'Hero flag was not explicitly rejected without component mode'}
$global:LASTEXITCODE=0 # The final unit rejection is an expected negative test.
"PASS:18slots,17used,8x2walk+1staticidle; sourcecols0/3; idleSE0 duplicated; unselected-frame rowground retained; unitflag rejected. Fixture: $directory"
