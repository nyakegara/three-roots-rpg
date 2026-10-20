$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$directory = Join-Path $repo 'docs/art/animations'
$manifest = Get-Content (Join-Path $directory 'manifest.json') -Raw | ConvertFrom-Json
$bundle = [ordered]@{}
foreach ($sheet in $manifest.sheets) {
    $metadataPath = [IO.Path]::GetFullPath((Join-Path $directory $sheet))
    $metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
    $imagePath = Join-Path (Split-Path $metadataPath) $metadata.image
    $bundle[$sheet] = @{ meta = $metadata; image = 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($imagePath)) }
}
$json = ConvertTo-Json -InputObject $bundle -Depth 100 -Compress
# Escape script-delimiter characters even if future metadata contains user text.
$json = $json.Replace('<', '\u003c').Replace('>', '\u003e').Replace('&', '\u0026')
$template = Get-Content (Join-Path $directory 'index.html') -Raw
$marker = '<script>'
if ($template.IndexOf($marker, [StringComparison]::Ordinal) -lt 0 -or
    $template.IndexOf($marker, $template.IndexOf($marker, [StringComparison]::Ordinal) + $marker.Length, [StringComparison]::Ordinal) -ge 0) {
    throw 'Expected one gallery script marker.'
}
$portable = $template.Replace($marker, "$marker`nwindow.animationBundle=$json;")
[IO.File]::WriteAllText((Join-Path $directory 'preview.html'), $portable, [Text.UTF8Encoding]::new($false))
Write-Host "Portable preview: $($manifest.sheets.Count) sheets embedded."
