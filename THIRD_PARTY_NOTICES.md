# Third-party assets and components

Claude, OpenAI/Codex and OpenCode logos come from [The SVG](https://github.com/GLINCKER/thesvg/tree/main/public/icons). Its MIT license is reproduced in `docs/thesvg-LICENSE.txt`. Trademarks belong to their owners; TokenDock is unaffiliated.

The cat, Music/Clipboard icons and app icon were generated with imagegen. Camera and playback geometry is drawn in code. User artwork/photos are not distributed. Documentation artwork is a synthetic illustration drawn by MediaDemo.cs.

The notch concept is inspired by [boring.notch](https://github.com/TheBoredTeam/boring.notch). No code from that project is included. The Swift camera helper in `desktop/native/` was written for TokenDock.

Self-contained downloads include Microsoft .NET runtime notices. Windows media, camera and clipboard APIs remain system components.

The native desktop port uses [Avalonia](https://github.com/AvaloniaUI/Avalonia) (MIT), its Fluent theme/headless test backend, and the transitive SkiaSharp/HarfBuzzSharp rendering stack. SkiaSharp and HarfBuzzSharp bindings are MIT; native Skia uses BSD-style terms and HarfBuzz uses its upstream permissive license. Preserve dependency and runtime notices in distribution packages.

Linux camera capture dynamically links the system's GStreamer libraries (LGPL); music calls the separately installed [playerctl](https://github.com/altdesktop/playerctl) (LGPL). These tools/libraries are not vendored in the repository. The isolated music test uses dbus-next (MIT), only on CI. macOS AVFoundation, AppKit, AppleScript and CoreVideo are operating-system components. Apple Music/Spotify artwork is user-session data and is not redistributed.

## MediaRemote Adapter (native macOS development port)

Vendored from https://github.com/ungive/mediaremote-adapter at commit `0e82bf1c44ec39554fbfc402ec88a48c6ae2a0fd`. Copyright (c) 2025 Jonas van den Berg and contributors. BSD-3-Clause; full license is in `desktop/native/vendor/mediaremote-adapter/LICENSE` and shipped as `native/MediaRemote-LICENSE.txt`. Built from source; no downloaded executable.

## Avalonia macOS accessibility patch

The macOS native backend is built from Avalonia 12.1.4 with the action-advertisement patch described in `desktop/native/avalonia/README.md`. Upstream is MIT licensed; the full license is in `desktop/native/avalonia/LICENSE` and accompanies macOS packages.
