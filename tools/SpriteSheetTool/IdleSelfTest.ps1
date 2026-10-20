$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = Join-Path $repo 'docs/art/animations/sources/flora-thorn-guard.idle.source.png'
$output = Join-Path ([IO.Path]::GetTempPath()) 'three-roots-idle-test.png'
$tool = Join-Path $PSScriptRoot 'bin/Debug/net10.0-windows/SpriteSheetTool.dll'
dotnet $tool --components --source $source --output $output --kind idle --id idle-test `
 --content-box 8,8,48,64 --alpha-cutoff 128 --align-bottom 72 --frame-ms 260
if ($LASTEXITCODE -ne 0) { throw 'Idle export failed.' }
$meta = Get-Content ([IO.Path]::ChangeExtension($output, '.json')) -Raw | ConvertFrom-Json
if ($meta.columns -ne 4 -or $meta.rows -ne 1 -or $meta.width -ne 256 -or $meta.height -ne 80 -or
 $meta.clips.Count -ne 1 -or $meta.clips[0].name -ne 'idle' -or !$meta.clips[0].loop -or
 $meta.clips[0].frames.Count -ne 4 -or $meta.clips[0].impactFrame -ne $null) {
 throw 'Wrong idle sheet or clip contract.'
}
if (@($meta.frameDiagnostics | Where-Object { $_.empty -or $_.edgePixels -gt 0 }).Count -gt 0) {
 throw 'Idle contains empty or edge-touching frames.'
}
Write-Host 'PASS: four-frame looping idle export.'
