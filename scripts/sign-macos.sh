#!/usr/bin/env bash
# Sign nested code inside-out; never use --deep to sign.
set -euo pipefail
[[ "$(uname -s)" == Darwin ]] || { echo 'macOS required' >&2; exit 2; }
root="$(cd "$(dirname "$0")/.." && pwd)"
bundle="${1:?Usage: sign-macos.sh APP_BUNDLE}"
[[ -d "$bundle/Contents/MacOS/native/TokenDockCamera.app" && -f "$bundle/Contents/MacOS/TokenDock" ]] || exit 2
identity="${MACOS_APP_IDENTITY:--}"
flags=(--force --sign "$identity" --options runtime)
if [[ "$identity" == - ]]; then
  flags+=(--timestamp=none)
else
  [[ "$identity" == 'Developer ID Application: '* ]] || { echo 'Use a Developer ID Application identity.' >&2; exit 2; }
  flags+=(--timestamp)
fi
# Include the runtime, Skia, Avalonia and MediaRemote binaries, not only the apphost.
while IFS= read -r -d '' binary; do
  [[ "$binary" == "$bundle/Contents/MacOS/TokenDock" ]] && continue
  if file -b "$binary" | grep -q 'Mach-O'; then
    codesign "${flags[@]}" "$binary"
  fi
done < <(find "$bundle/Contents" -type f -print0)
while IFS= read -r -d '' framework; do codesign "${flags[@]}" "$framework"; done < <(find "$bundle/Contents" -depth -type d -name '*.framework' -print0)
codesign "${flags[@]}" --entitlements "$root/desktop/native/camera-entitlements.plist" "$bundle/Contents/MacOS/native/TokenDockCamera.app"
codesign "${flags[@]}" --entitlements "$root/desktop/native/entitlements.plist" "$bundle"
codesign --verify --deep --strict "$bundle"
codesign -d --entitlements :- "$bundle" > /dev/null
# Verification does not establish Apple trust for an ad-hoc build.
echo "PASS: nested macOS code signed and verified (identity: $identity)."
