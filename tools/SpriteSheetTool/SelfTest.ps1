param([string]$ToolDll = (Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'))
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Add-Type -AssemblyName System.Drawing
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('sprite-path-selftest-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
function New-Fixture([string]$Path, [int]$Blue) {
 New-Item -ItemType Directory -Force -Path (Split-Path $Path) | Out-Null
 $bitmap=[System.Drawing.Bitmap]::new(160,160)
 for($row=0;$row -lt 2;$row++){for($col=0;$col -lt 4;$col++){for($y=10;$y -lt 60;$y++){for($x=10;$x -lt 30;$x++){$bitmap.SetPixel($col*40+$x,$row*80+$y,[System.Drawing.Color]::FromArgb(255,60+$col*30,90+$row*40,$Blue))}}}}
 $bitmap.Save($Path,[System.Drawing.Imaging.ImageFormat]::Png);$bitmap.Dispose()
}
$cases=@('copy-output','contact-source','contact-output','copy-metadata','contact-metadata','output-source','output-secondary','output-rowmap','metadata-source','metadata-secondary','copy-secondary','contact-secondary','copy-rowmap','contact-rowmap','copy-contact','output-normalized-source')
$checks=0
foreach($mode in @('grid','components')) {
 foreach($case in $cases) {
  $dir=Join-Path $testRoot "$mode-$case";New-Item -ItemType Directory -Path $dir | Out-Null
  $source=Join-Path $dir 'source.png';$secondary=Join-Path $dir 'secondary.png';$rowmap=Join-Path $dir 'rows.json';$output=Join-Path $dir 'exports/result.png';$copy=$null;$contact=$null
  if($case -eq 'metadata-source'){$source=Join-Path $dir 'exports/result.json'}
  if($case -eq 'metadata-secondary'){$secondary=Join-Path $dir 'exports/result.json'}
  New-Fixture $source 70;New-Fixture $secondary 140
  @(@{source=$secondary;sourceRow=0;targetRow=0;sourceColumns=4;sourceRows=2})|ConvertTo-Json -AsArray|Set-Content $rowmap
  switch($case) {
   'copy-output'{$copy=$output} 'contact-source'{$contact=$source} 'contact-output'{$contact=$output}
   'copy-metadata'{$copy=[System.IO.Path]::ChangeExtension($output,'.json')} 'contact-metadata'{$contact=[System.IO.Path]::ChangeExtension($output,'.json')}
   'output-source'{$output=$source} 'output-secondary'{$output=$secondary} 'output-rowmap'{$output=$rowmap}
   'copy-secondary'{$copy=$secondary} 'contact-secondary'{$contact=$secondary} 'copy-rowmap'{$copy=$rowmap} 'contact-rowmap'{$contact=$rowmap}
   'copy-contact'{$copy=Join-Path $dir 'shared.png';$contact=$copy}
   'output-normalized-source'{New-Item -ItemType Directory -Path (Join-Path $dir 'nested')|Out-Null;$output=Join-Path $dir 'nested/../source.png'}
  }
  $hashes=@{};foreach($inputPath in @($source,$secondary,$rowmap)){$hashes[$inputPath]=(Get-FileHash -LiteralPath $inputPath).Hash}
  $arguments=@('--source',$source,'--output',$output,'--kind','unit','--id','selftest','--row-map',$rowmap,'--alpha-cutoff','128')
  if($mode -eq 'components'){$arguments+='--components'};if($copy){$arguments+=@('--source-copy',$copy)};if($contact){$arguments+=@('--contact-strip',$contact)}
  $log=& dotnet $ToolDll @arguments 2>&1;$code=$LASTEXITCODE
  foreach($inputPath in $hashes.Keys){if((Get-FileHash -LiteralPath $inputPath).Hash -ne $hashes[$inputPath]){throw "$mode/$case CHANGED INPUT $inputPath"}}
  if($code -eq 0){throw "$mode/$case was accepted; expected path-collision rejection. $log"}
  if(($log -join ' ') -notmatch 'Path collision'){throw "$mode/$case failed for an unrelated reason: $log"}
  foreach($path in @($output,[System.IO.Path]::ChangeExtension($output,'.json'),$copy,$contact)){
   if($path -and !(($hashes.Keys|ForEach-Object {[System.IO.Path]::GetFullPath($_)}) -contains [System.IO.Path]::GetFullPath($path)) -and (Test-Path -LiteralPath $path)){throw "$mode/$case wrote $path before rejection"}
  }
  $checks++
 }
 foreach($archive in @($false,$true)) {
  $dir=Join-Path $testRoot "$mode-positive-$archive";New-Item -ItemType Directory -Path $dir|Out-Null
  $source=Join-Path $dir 'source.png';New-Fixture $source 90;$hash=(Get-FileHash $source).Hash;$output=Join-Path $dir 'exports/result.png'
  $arguments=@('--source',$source,'--output',$output,'--kind','unit','--id','selftest','--alpha-cutoff','128');if($mode -eq 'components'){$arguments+='--components'}
  if($archive){$arguments+=@('--source-copy',$source)} # Explicit identical-source archive is a no-op.
  $log=& dotnet $ToolDll @arguments 2>&1;if($LASTEXITCODE -ne 0){throw "Positive export failed: $log"}
  if((Get-FileHash $source).Hash -ne $hash){throw 'Positive export changed input'}
  $meta=Get-Content ([System.IO.Path]::ChangeExtension($output,'.json')) -Raw|ConvertFrom-Json
  if([System.IO.Path]::GetFullPath((Join-Path (Split-Path $output) $meta.source.image)) -ne $source){throw 'source.image does not resolve to actual input'}
  $checks++
 }
}
foreach($scaleCase in @('oversize','conflict','valid')) {
 $dir=Join-Path $testRoot "scale-$scaleCase";New-Item -ItemType Directory -Path $dir|Out-Null
 $source=Join-Path $dir 'source.png';New-Fixture $source 80;$hash=(Get-FileHash $source).Hash
 $first=if($scaleCase -eq 'oversize'){100.0}else{0.5};$second=if($scaleCase -eq 'conflict'){0.6}else{$first}
 $rowmap=Join-Path $dir 'rows.json';@(@{source=$source;sourceRow=0;targetRow=0;sourceColumns=4;sourceRows=2;sourceScale=$first},@{source=$source;sourceRow=1;targetRow=1;sourceColumns=4;sourceRows=2;sourceScale=$second})|ConvertTo-Json|Set-Content $rowmap
 $output=Join-Path $dir 'result.png';$log=& dotnet $ToolDll --components --source $source --output $output --kind unit --id selftest.scale --row-map $rowmap --alpha-cutoff 128 2>&1;$code=$LASTEXITCODE
 if((Get-FileHash $source).Hash -ne $hash){throw 'Scale request changed source'}
 if($scaleCase -eq 'valid') {
  if($code -ne 0){throw "Valid global scale failed: $log"};$meta=Get-Content ([IO.Path]::ChangeExtension($output,'.json')) -Raw|ConvertFrom-Json
  if(@($meta.unpacking.frames|Where-Object {$_.scale -ne 0.5}).Count){throw 'Source scale differs between frames'}
 }else{
  if($code -eq 0 -or ($log -join ' ') -notmatch 'sourceScale' -or (Test-Path $output)){throw "Invalid scale accepted or wrote output: $log"}
 }
 $checks++
}
"PASS: $checks collision/positive/source-scale cases; all source and row-map SHA256 hashes unchanged. Fixtures: $testRoot"
