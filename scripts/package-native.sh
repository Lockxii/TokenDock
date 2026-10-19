#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
runtime="${1:?Usage: package-native.sh linux-x64|linux-arm64|osx-x64|osx-arm64}"
case "$runtime" in linux-x64|linux-arm64|osx-x64|osx-arm64) ;; *) exit 2;; esac
host_os=linux
[[ "$(uname -s)" == Darwin ]] && host_os=osx
host_arch=x64
[[ "$(uname -m)" == arm64 || "$(uname -m)" == aarch64 ]] && host_arch=arm64
if [[ "$runtime" != "$host_os-$host_arch" ]]; then
  echo "Build on a matching $runtime host so the native camera helper cannot have the wrong architecture." >&2; exit 2
fi
out="$root/artifacts/native-$runtime"
if [[ -e "$out" ]]; then echo "Refusing to overwrite an existing package directory." >&2; exit 1; fi
mkdir -p "$out"
dotnet publish desktop -c Release -r "$runtime" --self-contained true -o "$out/publish"
bash desktop/native/build.sh "$out/publish"
cp LICENSE "$out/publish/TokenDock-LICENSE.txt"
cp THIRD_PARTY_NOTICES.md "$out/publish/TokenDock-NOTICES.md"
cp desktop/README.md "$out/publish/DEVELOPMENT.md"
if [[ "$runtime" == osx-* ]]; then
  bundle="$out/TokenDock.app"
  mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
  cp -R "$out/publish/." "$bundle/Contents/MacOS/"
  cp desktop/native/Info.plist "$bundle/Contents/Info.plist"
  iconset="$out/TokenDock.iconset"
  mkdir -p "$iconset"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" assets/tokendock-icon.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z "$double" "$double" assets/tokendock-icon.png --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$iconset" -o "$bundle/Contents/Resources/TokenDock.icns"
  # Ad-hoc signing is only for local development. It is not notarization.
  codesign --force --sign - "$bundle/Contents/MacOS/native/TokenDockCamera.app"
  codesign --force --deep --sign - --entitlements desktop/native/entitlements.plist "$bundle"
  codesign --verify --deep --strict "$bundle"
  "$bundle/Contents/MacOS/TokenDock" --check "$out/package-checks"
  # Development distribution containers; no Developer ID or notarization claim.
  stage="$out/dmg-root"
  mkdir -p "$stage"
  cp -R "$bundle" "$stage/TokenDock.app"
  ln -s /Applications "$stage/Applications"
  hdiutil create -volname TokenDock -srcfolder "$stage" -format UDZO "$out/TokenDock-$runtime.dmg"
  mkdir "$out/pkg-root"
  cp -R "$bundle" "$out/pkg-root/TokenDock.app"
  pkgbuild --analyze --root "$out/pkg-root" "$out/components.plist"
  /usr/libexec/PlistBuddy -c 'Set :0:BundleIsRelocatable false' "$out/components.plist"
  pkgbuild --root "$out/pkg-root" --component-plist "$out/components.plist" --install-location /Applications --identifier io.github.lockxii.tokendock --version 0.3.0 "$out/TokenDock-$runtime.pkg"
else
  "$out/publish/TokenDock" --check "$out/package-checks"
  stage="$out/deb-root"
  mkdir -p "$stage/DEBIAN" "$stage/usr/lib/tokendock" "$stage/usr/bin" "$stage/usr/share/applications" "$stage/usr/share/pixmaps"
  cp -a "$out/publish/." "$stage/usr/lib/tokendock/"
  printf '%s\n' '#!/bin/sh' 'exec /usr/lib/tokendock/TokenDock "$@"' > "$stage/usr/bin/tokendock"
  chmod 755 "$stage/usr/bin/tokendock"
  cp assets/tokendock-icon.png "$stage/usr/share/pixmaps/tokendock.png"
  printf '%s\n' '[Desktop Entry]' 'Type=Application' 'Name=TokenDock' 'Exec=/usr/bin/tokendock' 'Icon=tokendock' 'Terminal=false' 'Categories=Utility;' > "$stage/usr/share/applications/tokendock.desktop"
  arch=amd64
  [[ "$runtime" == linux-arm64 ]] && arch=arm64
  cat > "$stage/DEBIAN/control" <<EOF
Package: tokendock
Version: 0.3.0~dev.1
Architecture: $arch
Maintainer: TokenDock contributors <noreply@github.com>
Section: utils
Priority: optional
Installed-Size: $(du -sk "$stage/usr" | cut -f1)
Depends: libicu74 | libicu78 | libicu76 | libicu72, libssl3t64 | libssl3, libc6 (>= 2.35), libgcc-s1, libstdc++6, zlib1g, libfontconfig1, libice6, libsm6, libx11-6, libxrandr2, libxi6, libxcursor1, libsqlite3-0, libgstreamer1.0-0, gstreamer1.0-plugins-base, gstreamer1.0-plugins-good, playerctl
Description: TokenDock native development build
 Inline token, media, clipboard, RAM and photo modules. Not a supported release.
EOF
  chmod 755 "$stage/DEBIAN"
  chmod 644 "$stage/DEBIAN/control"
  dpkg-deb --build --root-owner-group "$stage" "$out/TokenDock-$runtime.deb"
  tar -czf "$out/TokenDock-$runtime.tar.gz" -C "$out/publish" .
fi
echo "Development package only: $out. Native parity and distribution validation gates remain closed."
