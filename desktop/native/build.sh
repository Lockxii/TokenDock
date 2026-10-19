#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:?Usage: build.sh OUTPUT_DIRECTORY}"
mkdir -p "$out/native"
case "$(uname -s)" in
 Darwin)
  swiftc -swift-version 5 -O "$root/desktop/native/Camera.swift" -o "$out/native/tokendock-camera" -framework AVFoundation -framework CoreVideo
  ;;
 Linux)
  cc -Wall -Wextra -Werror -O2 "$root/desktop/native/camera-linux.c" -o "$out/native/tokendock-camera" $(pkg-config --cflags --libs gstreamer-app-1.0 gstreamer-video-1.0)
  ;;
 *) echo "This helper is for Linux and macOS." >&2; exit 2;;
esac
"$out/native/tokendock-camera" --self-test > "$out/native/frame.fixture"
python3 - "$out/native/frame.fixture" <<'PY'
import pathlib, struct, sys
data = pathlib.Path(sys.argv[1]).read_bytes()
assert struct.unpack('<III', data[:12]) == (2, 2, 16)
assert len(data) == 28
print('PASS: native camera transport fixture; no camera activated')
PY
