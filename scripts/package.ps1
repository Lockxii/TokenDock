param([ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')][string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$run = Join-Path $root ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $run 'publish'
$bundle = Join-Path $run 'TokenDock'
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
dotnet publish (Join-Path $root 'next/TokenDock.Next.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$check = Start-Process -FilePath (Join-Path $publish 'TokenDock.Next.exe') -ArgumentList '--check' -Wait -PassThru -WindowStyle Hidden
if ($check.ExitCode -ne 0) { throw 'Checks failed' }
Get-Content (Join-Path $publish 'checks.txt')
# Explicit runtime allowlist: no generated test screenshots, logs or fixtures.
Get-ChildItem $publish -File | Where-Object { $_.Extension -in '.exe','.dll','.json' -or $_.Name -in 'LICENSE.txt','THIRD-PARTY-NOTICES.txt' } | Copy-Item -Destination $bundle
Copy-Item (Join-Path $publish 'assets') -Destination $bundle -Recurse
Copy-Item (Join-Path $root 'README.md'),(Join-Path $root 'LICENSE'),(Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $bundle
Copy-Item (Join-Path $root 'docs') -Destination $bundle -Recurse
$zip = Join-Path $run "TokenDock-v$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $bundle '*') -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $run 'SHA256SUMS.txt'), "$hash  $([IO.Path]::GetFileName($zip))`n")
Write-Output "Release files: $run"
