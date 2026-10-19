#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:?Usage: build.sh OUTPUT_DIRECTORY}"
mkdir -p "$out/native"
case "$(uname -s)" in
 Darwin)
  bash "$root/scripts/build-avalonia-macos.sh" "$out"
  source="$root/desktop/native/vendor/mediaremote-adapter"
  build="$root/artifacts/mediaremote-build"
  cmake -S "$source" -B "$build" -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0
  cmake --build "$build" --config Release --parallel 2
  cp -R "$build/MediaRemoteAdapter.framework" "$out/native/"
  cp "$source/bin/mediaremote-adapter.pl" "$out/native/"
  cp "$source/LICENSE" "$out/native/MediaRemote-LICENSE.txt"
  bundle="$out/native/TokenDockCamera.app"
  mkdir -p "$bundle/Contents/MacOS"
  cp "$root/desktop/native/Camera-Info.plist" "$bundle/Contents/Info.plist"
  helper="$bundle/Contents/MacOS/tokendock-camera"
  swiftc -swift-version 5 -target "$(uname -m)-apple-macosx15.0" -O "$root/desktop/native/Camera.swift" -o "$helper" -framework AVFoundation -framework CoreVideo
  codesign --force --sign - "$bundle"
  "$helper" --permission-status
  ;;
 Linux)
  helper="$out/native/tokendock-camera"
  cc -Wall -Wextra -Werror -O2 "$root/desktop/native/camera-linux.c" -o "$helper" $(pkg-config --cflags --libs gstreamer-app-1.0 gstreamer-video-1.0)
  ;;
 *) echo "This helper is for Linux and macOS." >&2; exit 2;;
esac
"$helper" --self-test > "$out/native/frame.fixture"
python3 - "$out/native/frame.fixture" <<'PY'
import pathlib, struct, sys
data = pathlib.Path(sys.argv[1]).read_bytes()
assert struct.unpack('<III', data[:12]) == (2, 2, 16)
assert len(data) == 28
print('PASS: native camera transport fixture; no camera activated')
PY
