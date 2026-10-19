#!/usr/bin/env bash
# Explicit submission only; the normal CI never supplies a profile or submits.
set -euo pipefail
[[ "$(uname -s)" == Darwin ]] || exit 2
artifact="${1:?Usage: notarize-macos.sh APP_OR_DMG_OR_PKG}"
profile="${MACOS_NOTARY_PROFILE:?Configure a notarytool keychain profile first}"
[[ -e "$artifact" ]] || exit 2
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
upload="$artifact"
case "$artifact" in
  *.app)
    codesign --verify --deep --strict "$artifact"
    codesign -dv "$artifact" 2>&1 | grep -q 'Authority=Developer ID Application:' || { echo 'Developer ID signature required.' >&2; exit 2; }
    upload="$scratch/TokenDock.zip"
    ditto -c -k --keepParent "$artifact" "$upload" ;;
  *.pkg) pkgutil --check-signature "$artifact" ;;
  *.dmg) codesign --verify --strict "$artifact" ;;
  *) echo 'Expected .app, .pkg or .dmg' >&2; exit 2 ;;
esac
xcrun notarytool submit "$upload" --keychain-profile "$profile" --wait --timeout 30m --output-format json > "$scratch/result.json"
python3 - "$scratch/result.json" <<'PY'
import json,sys
result=json.load(open(sys.argv[1]))
print('Notarization submission:',result.get('id'), 'status:',result.get('status'))
if result.get('status') != 'Accepted':
    raise SystemExit('Not accepted: inspect this submission with notarytool log; no release permitted.')
PY
xcrun stapler staple "$artifact"
xcrun stapler validate "$artifact"
case "$artifact" in
 *.app) spctl --assess --type execute --verbose=2 "$artifact" ;;
 *.pkg) spctl --assess --type install --verbose=2 "$artifact" ;;
 *.dmg) spctl --assess --type open --context context:primary-signature --verbose=2 "$artifact" ;;
esac
