param(
    [Parameter(Mandatory = $true)][string]$PreviewDirectory,
    [string]$NewMapPath = (Join-Path $PSScriptRoot 'map-comparison-source.png')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$preview = (Resolve-Path -LiteralPath $PreviewDirectory).Path
$assembly = Join-Path $preview 'Game.MonoGame.dll'
$art = Join-Path $preview 'Content/art'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Build the dedicated preview directory first.' }
if ($preview.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a separate build directory outside this checkout.'
}
$frames = Join-Path $PSScriptRoot '../screenshots'
$tiles = @('nw', 'ne', 'sw', 'se')
$originals = @{}
foreach ($tile in $tiles) {
    $path = Join-Path $art "exploration.flora-island-v5-$tile.png"
    $originals[$path] = [IO.File]::ReadAllBytes($path)
}
function Capture([string]$name, [string]$mode) {
    $output = [IO.Path]::GetFullPath((Join-Path $frames "map-$name.png"))
    & dotnet $assembly $mode $output
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) {
        throw "Capture failed: $name (exit code $exitCode)."
    }
}
try {
    Capture 'current-close' '--screenshot-after-choice'
    Capture 'current-overview' '--screenshot-map-after-choice'
    $image = [Drawing.Bitmap]::new((Resolve-Path -LiteralPath $NewMapPath).Path)
    try {
        if ($image.Width % 2 -or $image.Height % 2) { throw 'Map dimensions must be even.' }
        $width = $image.Width / 2
        $height = $image.Height / 2
        for ($index = 0; $index -lt 4; $index++) {
            $rectangle = [Drawing.Rectangle]::new(($index % 2) * $width, [Math]::Floor($index / 2) * $height, $width, $height)
            $part = $image.Clone($rectangle, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $part.Save((Join-Path $art "exploration.flora-island-v5-$($tiles[$index]).png"), [Drawing.Imaging.ImageFormat]::Png)
            } finally { $part.Dispose() }
        }
    } finally { $image.Dispose() }
    Capture 'replacement-close' '--screenshot-after-choice'
    Capture 'replacement-overview' '--screenshot-map-after-choice'
} finally {
    foreach ($path in $originals.Keys) { [IO.File]::WriteAllBytes($path, $originals[$path]) }
}
