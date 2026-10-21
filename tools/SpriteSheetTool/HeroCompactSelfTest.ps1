param([string]$ToolDll=(Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'))
$ErrorActionPreference='Stop';$PSNativeCommandUseErrorActionPreference=$false
Add-Type -AssemblyName System.Drawing
$directory=Join-Path ([IO.Path]::GetTempPath()) ('sprite-hero-compact-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $directory|Out-Null
$source=Join-Path $directory 'walk.png';$bitmap=[System.Drawing.Bitmap]::new(240,720)
for($row=0;$row -lt 9;$row++){for($column=0;$column -lt 6;$column++){
 $color=[System.Drawing.Color]::FromArgb(255,10+$column*30,20+$row*20,80)
 $bottom=if($column -eq 5){70}else{60}
 for($y=10;$y -lt $bottom;$y++){for($x=10;$x -lt 30;$x++){$bitmap.SetPixel($column*40+$x,$row*80+$y,$color)}}
}}
$bitmap.Save($source,[System.Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose()
$idleSource=Join-Path $directory 'neutral.png';$bitmap=[System.Drawing.Bitmap]::new(40,80);$neutral=[System.Drawing.Color]::FromArgb(255,250,180,20)
for($y=10;$y -lt 45;$y++){for($x=12;$x -lt 28;$x++){$bitmap.SetPixel($x,$y,$neutral)}}
for($y=44;$y -lt 65;$y++){foreach($x in (@(10..16)+@(23..29))){$bitmap.SetPixel($x,$y,$neutral)}}
$bitmap.Save($idleSource,[System.Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose()
$hash=(Get-FileHash $source).Hash;$idleHash=(Get-FileHash $idleSource).Hash
$map=Join-Path $directory 'rows.json';@(@{source=$idleSource;sourceRow=0;targetRow=8;sourceColumns=1;sourceRows=1})|ConvertTo-Json -AsArray|Set-Content $map
$output=Join-Path $directory 'hero.png'
$arguments=@('--components','--hero-compact-walk','--anchor-mode','cell','--source',$source,'--output',$output,'--kind','hero','--id','compact.fixture','--row-map',$map,'--content-box','0,8,64,64','--alpha-cutoff','128')
$log=& dotnet $ToolDll @arguments 2>&1;if($LASTEXITCODE -ne 0){throw "Compact export failed: $log"}
$meta=Get-Content ([IO.Path]::ChangeExtension($output,'.json')) -Raw|ConvertFrom-Json
if($meta.width -ne 256 -or $meta.height -ne 720 -or $meta.columns -ne 4 -or $meta.rows -ne 9 -or $meta.unpacking.frames.Count -ne 36 -or $meta.usedFrameCount -ne 29){throw 'Expected4x9/36slots/29used'}
$used=@();for($row=0;$row -lt 8;$row++){$clip=$meta.clips[$row];$count=if($row -in @(0,4)){2}else{4};$ms=if($count -eq 2){220}else{110};if($clip.frames.Count -ne $count -or $clip.frameMs -ne $ms -or !$clip.loop){throw 'Wrong compact gait'};for($index=0;$index -lt $count;$index++){if($clip.frames[$index] -ne $row*4+$index){throw 'Wrong clip slot'}};$used+=$clip.frames}
$idle=$meta.clips[8];if($idle.frames.Count -ne 1 -or $idle.frames[0] -ne 32 -or $idle.loop){throw 'Idle must use only slot32'};$used+=$idle.frames
if(@($used|Select-Object -Unique).Count -ne 29){throw 'Expected29usedframes'}
$image=[System.Drawing.Bitmap]::new($output)
try{
 for($frame=0;$frame -lt 36;$frame++){
  $row=[int][Math]::Floor($frame/4);$column=$frame%4;$record=$meta.unpacking.frames[$frame]
  $selected=if($row -eq 8){0}elseif($row -in @(0,4)){@([int]0,[int]3,[int]0,[int]3)[$column]}else{@([int]0,[int]1,[int]3,[int]4)[$column]}
  $expectedFrame=if($row -eq 8){0}else{$row*6+$selected}
  if($record.sourceFrame -ne $expectedFrame -or $record.sourceColumn -ne $selected){throw "Wrong source selection slot$frame"}
  $expected=if($row -eq 8){$neutral}else{[System.Drawing.Color]::FromArgb(255,10+$selected*30,20+$row*20,80)};$seen=0
  for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){$pixel=$image.GetPixel($column*64+$x,$row*80+$y);if(!$pixel.A){continue};$seen++;if($pixel.ToArgb() -ne $expected.ToArgb()){throw "Wrong frame RGB slot$frame"}}};if(!$seen){throw 'Empty frame'}
  if($row -eq 8){$info=$meta.unpacking.sources|Where-Object source -eq $record.source;if($info.Columns -ne 1 -or $info.Rows -ne 1){throw 'Idle did not use1x1source'}}
 }
 for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){
  $pixel=$image.GetPixel($x,640+$y).ToArgb();foreach($column in 1..3){if($pixel -ne $image.GetPixel($column*64+$x,640+$y).ToArgb()){throw 'Idle duplicates differ'}}
  foreach($row in @(0,4)){for($column=0;$column -lt 2;$column++){if($image.GetPixel($column*64+$x,$row*80+$y).ToArgb() -ne $image.GetPixel(($column+2)*64+$x,$row*80+$y).ToArgb()){throw 'N/S unused duplicates differ'}}}
 }}
}finally{$image.Dispose()}
@(@{source=$idleSource;sourceRow=0;targetRow=3;sourceColumns=1;sourceRows=1})|ConvertTo-Json -AsArray|Set-Content $map
$invalid=Join-Path $directory 'invalid-row.png'
$log=& dotnet $ToolDll --components --hero-compact-walk --source $source --output $invalid --kind hero --id invalid --row-map $map 2>&1
if($LASTEXITCODE -eq 0 -or (Test-Path $invalid) -or ($log -join ' ') -notmatch 'Invalid component row map'){throw '1x1source allowed outside idle row8'}
if((Get-FileHash $source).Hash -ne $hash -or (Get-FileHash $idleSource).Hash -ne $idleHash){throw 'Source changed'}
$global:LASTEXITCODE=0
"PASS:36slots/29used; N/S2,other6x4; source0/3 and0/1/3/4; staticneutral1x1idle32; duplicatesexact; 1x1rejectoutsideidle. Fixture: $directory"
