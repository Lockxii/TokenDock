# Native Linux and macOS port — development

This is an executable Avalonia/.NET 10 application, not a web mockup. It shares
token parsing, custom JSON feeds, preferences and the bounded file shelf with the
Windows edition. It is **not a supported release yet**. The Windows WPF app in
`next/` remains unchanged as the released interface.

## Implemented

- Borderless top/bottom/left/right/floating dock; tray restore; inline panels.
- Direct icon reordering, visual customization, persistent preferences and placement.
- Retargetable spring resizing, short hover switching and reduced-motion setting.
- Codex, Claude Code, OpenCode and custom JSON counters; featured counter and graph.
- Native RAM readings, process consumers and a cat with editable color/speed thresholds.
- Music artwork/title in the dock, inline controls, seek bar and timeline.
- Opt-in local Clipboard history, text/images/files, individual removal and copy feedback.
- Drag files into the temporary shelf; drag file/image cards out with copy semantics.
- Camera preview only while its panel is selected, no audio; round PNG photos with
  transparent corners, saved to Pictures/TokenDock and immediately placed first in Clipboard.
- Serialized camera sessions, bounded packets and UI backpressure; cancellation on
  switch, collapse, hide and exit. Native capture helpers cannot start without `--capture`.

### Platform adapters

| Integration | Linux | macOS |
| --- | --- | --- |
| UI/clipboard/tray/drop | Avalonia native X11 backend | Avalonia native Cocoa backend |
| Music | MPRIS via installed `playerctl` | System Now Playing via bundled MediaRemote adapter; AppleScript fallback for Music/Spotify |
| Camera | GStreamer `autovideosrc`, BGRA, 640×480 at 15 fps | AVFoundation video-only helper with its own permission bundle |
| Memory | `/proc/meminfo`, MemAvailable | Mach/sysctl, free + inactive estimate |
| SQLite | System libsqlite3.so.0 | System libsqlite3.dylib |

macOS reads the system-selected session, including browsers and third-party players
that publish Now Playing metadata. Apps that do not expose a session cannot be
controlled through this API. The adapter uses a private Apple framework; OS updates
can break it. Apple Music/Spotify remain fallback adapters. Linux native Wayland
placement, mixed-DPI display changes, external drag/drop target behavior,
accessibility and physical camera permissions/shutdown remain validation gates.
Do not infer full parity from a successful build or fixture screenshot.

## Build on the target OS

Install the .NET 10 SDK. On Debian/Ubuntu, install native dependencies:

```sh
sudo apt-get install build-essential pkg-config libgstreamer1.0-dev \
  libgstreamer-plugins-base1.0-dev gstreamer1.0-plugins-base \
  gstreamer1.0-plugins-good playerctl libfontconfig1 libice6 libsm6
```

The macOS development target is **macOS 15 or newer**, matching the current
[.NET 10 OS support policy](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

On macOS, install Xcode Command Line Tools (`xcode-select --install`) and CMake.
The pinned MediaRemote source is built locally; no precompiled adapter is downloaded.

```sh
dotnet publish desktop -c Release --no-self-contained -o artifacts/desktop
bash desktop/native/build.sh artifacts/desktop
dotnet artifacts/desktop/TokenDock.dll --check artifacts/desktop-renders
```

The last command uses synthetic data only; it does not activate the camera,
read the system clipboard, watch personal tool logs or open a desktop window.
To run the actual app intentionally after building:

```sh
dotnet artifacts/desktop/TokenDock.dll
```

Camera and clipboard history are disabled by default. Enable modules in Customize.
Music on macOS may prompt for Automation access to the selected running player.
Camera access is requested only when opening its panel; a denied request is shown
in the panel. Missing Linux plugins/playerctl produce an actionable status.

### Self-contained development bundles

Run on a host matching the requested architecture:

```sh
bash scripts/package-native.sh linux-x64
# Or linux-arm64, osx-arm64, osx-x64 on a matching host.
```

The script refuses to overwrite an existing package directory. It produces a
local development bundle, not a GitHub release. The macOS bundle is ad-hoc signed
for development; Developer ID signing/notarization has not been completed.

## Verification and publication gate

CI builds the desktop and native helpers on Linux/macOS, on x64 and ARM64, and runs:

- Shared-core checks against system memory/SQLite, with synthetic usage logs.
- All five dock placements and all panels; bounded history, round-photo alpha,
  original-file preservation, rapid retargeting and camera cancellation fixtures.
- MPRIS metadata and controls against a fake player in an isolated D-Bus session.
- macOS system Now Playing metadata, artwork, pause, seeking and track controls
  against a synthetic MediaPlayer session with no audio output.
- Native text/image/photo-file clipboard roundtrips on an isolated X11 display and
  a disposable macOS runner. These checks refuse to run outside marked CI sessions.
- Apple Music script compilation; no player is launched for this check.
- Self-contained package smoke checks, including the ad-hoc signed macOS bundle.

Camera self-tests send a fixed 2×2 packet and never enumerate or open devices.
CI uploads fixture renders only, not installable binaries. Physical-camera tests,
real player permissions, external drag targets, compositor behavior, accessibility,
resource measurements and clean installation are still required by
[the release gate](../docs/PLATFORMS.md). No automatic release workflow bypasses it.
