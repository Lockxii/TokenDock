#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
runtime="${1:?Usage: package-native.sh linux-x64|linux-arm64|osx-x64|osx-arm64}"
case "$runtime" in linux-x64|linux-arm64|osx-x64|osx-arm64) ;; *) exit 2;; esac
out="$root/artifacts/native-$runtime"
if [[ -e "$out" ]]; then echo "Refusing to overwrite an existing package directory." >&2; exit 1; fi
mkdir -p "$out"
dotnet publish desktop -c Release -r "$runtime" --self-contained true -o "$out/publish"
bash desktop/native/build.sh "$out/publish"
if [[ "$runtime" == osx-* ]]; then
  bundle="$out/TokenDock.app"
  mkdir -p "$bundle/Contents/MacOS"
  cp -R "$out/publish/." "$bundle/Contents/MacOS/"
  cp desktop/native/Info.plist "$bundle/Contents/Info.plist"
  # Ad-hoc signing is only for local development. It is not notarization.
  codesign --force --sign - "$bundle/Contents/MacOS/native/tokendock-camera"
  codesign --force --deep --sign - --entitlements desktop/native/entitlements.plist "$bundle"
  codesign --verify --deep --strict "$bundle"
else
  mkdir -p "$out/publish/share/applications"
  printf '%s\n' '[Desktop Entry]' 'Type=Application' 'Name=TokenDock' "Exec=$out/publish/TokenDock" 'Terminal=false' 'Categories=Utility;' > "$out/publish/share/applications/tokendock.desktop"
fi
echo "Development package only: $out. Native parity and distribution approval gates remain closed."
