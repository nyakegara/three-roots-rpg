param([string]$ToolDll=(Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'))
$ErrorActionPreference='Stop';$PSNativeCommandUseErrorActionPreference=$false
Add-Type -AssemblyName System.Drawing
$directory=Join-Path ([IO.Path]::GetTempPath()) ('sprite-cell-anchor-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory|Out-Null
$source=Join-Path $directory 'moving-feet.png';$bitmap=[System.Drawing.Bitmap]::new(240,240)
$torso=[System.Drawing.Color]::FromArgb(255,200,40,20);$feet=[System.Drawing.Color]::FromArgb(255,20,40,200)
for($row=0;$row -lt 2;$row++){for($column=0;$column -lt 4;$column++){
 for($y=20;$y -lt 60;$y++){for($x=20;$x -lt 40;$x++){$bitmap.SetPixel($column*60+$x,$row*120+$y,$torso)}}
 for($x=10;$x -lt 50;$x++){$bitmap.SetPixel($column*60+$x,$row*120+59,$torso)}
 $legX=if($column%2){43}else{11};$bottom=if($column%2){88}else{80}
 for($y=60;$y -lt $bottom;$y++){for($x=$legX;$x -lt $legX+6;$x++){$bitmap.SetPixel($column*60+$x,$row*120+$y,$feet)}}
}}
$bitmap.Save($source,[System.Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose();$hash=(Get-FileHash $source).Hash
$output=Join-Path $directory 'cell.png'
$log=& dotnet $ToolDll --components --source $source --output $output --kind unit --id anchor.fixture --anchor-mode cell --content-box 0,8,64,64 --alpha-cutoff 128 --align-bottom 72 2>&1
if($LASTEXITCODE -ne 0){throw "Cell export failed: $log"}
$metadata=Get-Content ([IO.Path]::ChangeExtension($output,'.json')) -Raw|ConvertFrom-Json;$native=[System.Drawing.Bitmap]::new($output)
try{
 for($row=0;$row -lt 2;$row++){
  $positions=@()
  for($column=0;$column -lt 4;$column++){
   $left=64;$right=-1;$top=80;$bottom=-1
   for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){if($native.GetPixel($column*64+$x,$row*80+$y).ToArgb() -eq $torso.ToArgb()){$left=[Math]::Min($left,$x);$right=[Math]::Max($right,$x);$top=[Math]::Min($top,$y);$bottom=[Math]::Max($bottom,$y)}}}
   $positions+="$left,$top,$right,$bottom"
  }
  if(@($positions|Select-Object -Unique).Count -ne 1){throw "Fixed torso moved with moving feet: $($positions -join '; ')"}
 }
 for($row=0;$row -lt 2;$row++){
  $expectedTorso=$null;$relativeX=$null;$offsetY=$null;$actualBottoms=@()
  for($column=0;$column -lt 4;$column++){
   $frame=$row*4+$column;$record=$metadata.unpacking.frames[$frame];$left=64;$right=-1;$top=80;$bottom=-1;$visibleBottom=0
   for($y=0;$y -lt 80;$y++){for($x=0;$x -lt 64;$x++){
    $pixel=$native.GetPixel($column*64+$x,$row*80+$y)
    if($pixel.A){$visibleBottom=[Math]::Max($visibleBottom,$y+1)}
    if($pixel.ToArgb() -eq $torso.ToArgb()){$left=[Math]::Min($left,$x);$right=[Math]::Max($right,$x);$top=[Math]::Min($top,$y);$bottom=[Math]::Max($bottom,$y)}
   }}
   $bounds="$left,$top,$right,$bottom"
   if($null -eq $expectedTorso){$expectedTorso=$bounds}elseif($bounds -ne $expectedTorso){throw "Fixed torso moved with feet at row$row col${column}: $bounds vs $expectedTorso"}
   $cellRelativeX=[double]$record.transform.offsetX+$column*60*[double]$record.scale
   if($null -eq $relativeX){$relativeX=$cellRelativeX;$offsetY=[double]$record.transform.offsetY}
   elseif([Math]::Abs($cellRelativeX-$relativeX) -gt 0.000001 -or [Math]::Abs([double]$record.transform.offsetY-$offsetY) -gt 0.000001){throw 'Transforms vary within a source row'}
   if($metadata.registration[$frame].translationY -ne 0){throw 'Cell mode performed per-frame vertical normalization'}
   if($metadata.registration[$frame].registeredBottomExclusive -ne $visibleBottom){throw 'Registered bottom metadata is not truthful'}
   if([Math]::Abs([double]$record.pivotX-($column+.5)*60) -gt 0.000001 -or $record.pivotY -ne $row*120+88){throw 'Cell/row anchor is not fixed'}
   $actualBottoms+=$visibleBottom
  }
  if(@($actualBottoms|Select-Object -Unique).Count -lt 2){throw 'Moving feet were normalized to a single baseline'}
 }
 if($metadata.anchorMode -ne 'cell'){throw 'Missing truthful anchorMode'}
 if((Get-FileHash $source).Hash -ne $hash){throw 'Source fixture changed'}
}finally{$native.Dispose()}
"PASS: fixed torso XY with moving feet; constant offsets per row; cellcenterX/rowgroundY; dy0; truthful varying bottoms; unchanged source. Fixture: $directory"
