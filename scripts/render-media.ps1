param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'artifacts/media-build'
$renders = Join-Path $root 'artifacts/media-renders'
$output = Join-Path $root 'docs/media'
dotnet publish (Join-Path $root 'next/TokenDock.Next.csproj') -c Release --no-self-contained -o $build
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet (Join-Path $build 'TokenDock.Next.dll') --demo $renders
if ($LASTEXITCODE -ne 0) { throw 'Demo rendering failed' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($name in @('hero','music','clipboard','camera','ram','customize')) {
    Copy-Item (Join-Path $renders "$name.png") -Destination $output -Force
}
ffmpeg -y -framerate 20 -i (Join-Path $renders 'frames/%04d.png') -filter_complex '[0:v]split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3' -loop 0 (Join-Path $output 'transitions.gif')
if ($LASTEXITCODE -ne 0) { throw 'GIF encoding failed (install FFmpeg in PATH)' }
Write-Output "Documentation media: $output"
