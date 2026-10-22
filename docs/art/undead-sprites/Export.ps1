# Deterministic packaging only: alpha bounds, nearest-neighbor sizing and a common baseline.
# The character artwork is generated with image_gen; sources remain unchanged.
Add-Type -AssemblyName System.Drawing
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$root = Split-Path $root -Parent
$art = Join-Path $root 'content/art'
$destination = Join-Path $art 'prototypes/undead'
$specs = @(
 @{Id='utburd'; Width=48; Height=66},
 @{Id='silat'; Width=56; Height=66},
 @{Id='nachzehrer'; Width=84; Height=70},
 @{Id='vetala'; Width=56; Height=72},
 @{Id='asura-preta'; Width=112; Height=108}
)
function Get-AlphaBounds([Drawing.Bitmap]$image) {
 $left=$image.Width; $top=$image.Height; $right=-1; $bottom=-1
 for($y=0;$y -lt $image.Height;$y++){ for($x=0;$x -lt $image.Width;$x++) {
  if($image.GetPixel($x,$y).A -ge 128){$left=[Math]::Min($left,$x);$top=[Math]::Min($top,$y);$right=[Math]::Max($right,$x);$bottom=[Math]::Max($bottom,$y)}
 }}
 if($right -lt 0){throw 'Empty source'}
 return [Drawing.Rectangle]::new($left,$top,$right-$left+1,$bottom-$top+1)
}
function Fit-Sprite([string]$path,[int]$width,[int]$height) {
 $source=[Drawing.Bitmap]::new($path)
 try {
  $crop=Get-AlphaBounds $source
  $ratio=[Math]::Min($width/[double]$crop.Width,$height/[double]$crop.Height)
  $w=[Math]::Max(1,[int][Math]::Round($crop.Width*$ratio));$h=[Math]::Max(1,[int][Math]::Round($crop.Height*$ratio))
  $sprite=[Drawing.Bitmap]::new(128,128)
  # Explicit point sampling gives one physical output pixel per logical game pixel.
  for($y=0;$y -lt $h;$y++){for($x=0;$x -lt $w;$x++){
   $sx=$crop.X+[Math]::Min($crop.Width-1,[int][Math]::Floor(($x+0.5)*$crop.Width/$w))
   $sy=$crop.Y+[Math]::Min($crop.Height-1,[int][Math]::Floor(($y+0.5)*$crop.Height/$h))
   $color=$source.GetPixel($sx,$sy)
   if($color.A -ge 128){$sprite.SetPixel(64-[int][Math]::Floor($w/2)+$x,120-$h+$y,[Drawing.Color]::FromArgb(255,$color.R,$color.G,$color.B))}
  }}
  return ,$sprite
 } finally {$source.Dispose()}
}
$metadata=@()
foreach($spec in $specs){
 $sprite=Fit-Sprite (Join-Path $PSScriptRoot "sources/$($spec.Id).png") $spec.Width $spec.Height
 try {$sprite.Save((Join-Path $destination ($spec.Id+'.png')),[Drawing.Imaging.ImageFormat]::Png)} finally {$sprite.Dispose()}
 $metadata+=@{id=$spec.Id;maxWidth=$spec.Width;maxHeight=$spec.Height;canvas=128;baseline=120;image="../../../content/art/prototypes/undead/$($spec.Id).png"}
}
$metadata | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'manifest.json') -Encoding utf8
$items=@(
 @{Name='Peasant';Path=(Join-Path $art 'unit.human.militia.png');Width=56;Height=66},
 @{Name='Cucumber';Path=(Join-Path $art 'unit.flora.thorn-guard.png');Width=56;Height=66},
 @{Name='Knight';Path=(Join-Path $art 'unit.human.banner-knight.png');Width=83;Height=94}
)
$preview=[Drawing.Bitmap]::new(2048,560)
$g=[Drawing.Graphics]::FromImage($preview)
$font=[Drawing.Font]::new('Arial',12)
$g.Clear([Drawing.Color]::FromArgb(65,72,55))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
$all=@()
foreach($item in $items){$all+=@{Name=$item.Name;Image=(Fit-Sprite $item.Path $item.Width $item.Height)}}
foreach($spec in $specs){$all+=@{Name=$spec.Id;Image=([Drawing.Bitmap]::new((Join-Path $destination ($spec.Id+'.png'))))}}
try {
 $g.DrawString('GAME SCALE 1x - existing units and new sprite candidates', $font,[Drawing.Brushes]::White,12,8)
 for($i=0;$i -lt $all.Count;$i++){
  $x=64+$i*240
  $g.DrawImage($all[$i].Image,[Drawing.Rectangle]::new($x,32,128,128))
  $g.DrawString($all[$i].Name,$font,[Drawing.Brushes]::White,$x,170)
  $g.DrawImage($all[$i].Image,[Drawing.Rectangle]::new($i*256,230,256,256))
 }
 $g.DrawString('2x NEAREST NEIGHBOR - same files, no smoothing. Common foot baseline. Art preview, not gameplay.', $font,[Drawing.Brushes]::White,12,520)
 $preview.Save((Join-Path $PSScriptRoot 'comparison.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {foreach($item in $all){$item.Image.Dispose()};$font.Dispose();$g.Dispose();$preview.Dispose()}
Write-Output 'Five 128x128 transparent prototypes exported; common baseline 120; comparison generated.'

