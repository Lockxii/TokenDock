#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
[[ "$(uname -s)" == Darwin ]] || exit 2
out="${1:?Usage: build-avalonia-macos.sh PUBLISH_DIRECTORY}"
revision=2d79ba55fae1b1e71c69f93a668080c9029e6a31
source="$root/artifacts/avalonia-$revision"
patch_file="$root/desktop/native/avalonia/accessibility-actions.patch"
[[ "$(grep -c 'Version="12.1.4"' "$root/desktop/TokenDock.Desktop.csproj")" == 3 ]] || { echo 'Review native patch when upgrading Avalonia.' >&2; exit 2; }
if [[ ! -d "$source/.git" ]]; then
  git init "$source"
  git -C "$source" remote add origin https://github.com/AvaloniaUI/Avalonia.git
  git -C "$source" fetch --depth 1 origin "$revision"
  git -C "$source" checkout --detach FETCH_HEAD
fi
[[ "$(git -C "$source" rev-parse HEAD)" == "$revision" ]] || exit 2
if ! git -C "$source" apply --reverse --check "$patch_file" 2>/dev/null; then
  git -C "$source" apply --check "$patch_file"
  git -C "$source" apply "$patch_file"
fi
(cd "$source" && dotnet build native/Avalonia.Native/Avalonia.Native.macOS.proj -c Release --nologo)
cp "$source/artifacts/native/Release/libAvalonia.Native.OSX.dylib" "$out/libAvaloniaNative.dylib"
cp "$root/desktop/native/avalonia/LICENSE" "$out/Avalonia-LICENSE.txt"
