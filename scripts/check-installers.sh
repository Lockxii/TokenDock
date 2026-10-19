#!/usr/bin/env bash
set -euo pipefail
[[ "${CI:-}" == true && "${TOKENDOCK_TEST_DISPLAY:-}" == isolated ]] || { echo "Installer tests require disposable CI." >&2; exit 2; }
root="$(cd "$(dirname "$0")/.." && pwd)"
runtime="${1:?Pass the matching runtime}"
case "$runtime" in linux-x64|linux-arm64|osx-x64|osx-arm64) ;; *) exit 2;; esac
out="$root/artifacts/native-$runtime"
if [[ "$runtime" == linux-* ]]; then
  if dpkg-query -W -f='${Status}' tokendock 2>/dev/null | grep -q 'install ok installed'; then echo "Refusing to replace an existing installation." >&2; exit 2; fi
  cleanup() { sudo dpkg --purge tokendock >/dev/null 2>&1 || true; }
  trap cleanup EXIT
  desktop-file-validate "$out/deb-root/usr/share/applications/tokendock.desktop"
  sudo apt-get install -y "$out/TokenDock-$runtime.deb"
  /usr/bin/tokendock --check "$out/installed-checks"
  # Reinstallation exercises replacement without an uninstall gap.
  sudo dpkg -i "$out/TokenDock-$runtime.deb"
  /usr/bin/tokendock --check "$out/reinstalled-checks"
  sudo dpkg --purge tokendock
  [[ ! -e /usr/bin/tokendock && ! -e /usr/lib/tokendock && ! -e /usr/share/applications/tokendock.desktop ]]
  trap - EXIT
  mkdir "$out/portable-extract"
  tar -xzf "$out/TokenDock-$runtime.tar.gz" -C "$out/portable-extract"
  "$out/portable-extract/TokenDock" --check "$out/portable-checks"
else
  [[ ! -e /Applications/TokenDock.app ]] || { echo "Refusing to replace an existing installation." >&2; exit 2; }
  mount="$out/mounted"
  mkdir "$mount"
  cleanup() {
    hdiutil detach "$mount" >/dev/null 2>&1 || true
    # Only this CI-created app is eligible for cleanup.
    if [[ -e /Applications/TokenDock.app/Contents/Info.plist ]] && [[ "$(/usr/libexec/PlistBuddy -c 'Print CFBundleIdentifier' /Applications/TokenDock.app/Contents/Info.plist)" == io.github.lockxii.tokendock ]]; then
      sudo rm -rf /Applications/TokenDock.app
    fi
    sudo pkgutil --forget io.github.lockxii.tokendock >/dev/null 2>&1 || true
  }
  trap cleanup EXIT
  hdiutil verify "$out/TokenDock-$runtime.dmg"
  hdiutil attach -readonly -nobrowse -mountpoint "$mount" "$out/TokenDock-$runtime.dmg"
  codesign --verify --deep --strict "$mount/TokenDock.app"
  "$mount/TokenDock.app/Contents/MacOS/TokenDock" --check "$out/dmg-checks"
  hdiutil detach "$mount"
  for attempt in installed reinstalled; do
    sudo installer -pkg "$out/TokenDock-$runtime.pkg" -target /
    codesign --verify --deep --strict /Applications/TokenDock.app
    /Applications/TokenDock.app/Contents/MacOS/TokenDock --check "$out/$attempt-checks"
    if [[ "$attempt" == installed ]]; then
      /Applications/TokenDock.app/Contents/MacOS/TokenDock --native-clipboard-check
      python3 desktop/native/mac-media-check.py /Applications/TokenDock.app/Contents/MacOS/TokenDock
    fi
  done
  cleanup
  [[ ! -e /Applications/TokenDock.app ]]
  trap - EXIT
fi
echo "PASS: development installer install/reinstall/remove and portable container; no normal app launch."
