# Linux and macOS: full-parity release gate

**No supported Linux/macOS application release yet.** The executable [Avalonia port](../desktop/README.md) now includes the dock, music, Clipboard/file shelf, RAM and native camera helpers. Shared-core and desktop/native fixture checks run on Linux/macOS. Real-world validation and the remaining coverage gaps below still prevent publication.

The owner requires full parity before publication. A token-only prototype, web mockup or compatibility-layer build does not qualify.

## Dependencies to replace

Avalonia replaces WPF/window/tray/clipboard integration in the port. Linux uses playerctl/MPRIS and GStreamer; macOS uses a bundled MediaRemote Now Playing adapter (with AppleScript fallback for Music/Spotify) and AVFoundation for camera capture. Shared counters use each OS's SQLite library; native memory adapters are implemented. Windows retains its existing WPF edition.

## Required parity — validation remains open

| Feature | Linux | macOS |
| --- | --- | --- |
| Dock/tray/placement | X11 and documented Wayland behavior | Menu bar, Spaces, fullscreen, safe areas |
| Scaling/displays | DPI, negative coordinates, hot-plug | Retina and mixed displays |
| Reordering/settings | Every module, persistence, cancel, keyboard | Same |
| Counters | Native paths, read-only SQLite, incremental events | Same |
| RAM/cat | Linux memory/process semantics | Mach/sysctl memory/process semantics |
| Music | MPRIS artwork, commands and seeking | Native player integration and permissions |
| Clipboard | Text/images/files, opt-in history, external drag/drop | NSPasteboard and native drag/drop |
| File shelf | Bounded copies, cleanup, originals preserved | Same |
| Camera | Device/portal permissions | AVFoundation permissions and usage description |
| Photos | Round alpha PNG, auto-open Clipboard, export | Same |
| Privacy | Capture released on hide/close/switch/races | Same, including denied permission |
| Motion/accessibility | Retargeting, reduced motion, keyboard, labels | Same |
| Distribution | Installable artifacts and notices | Architectures, signing/notarization, packaging |

Wayland may restrict positioning. Resolve or explicitly agree limitations before claiming full parity.

## Evidence before release

1. Builds and integration checks on every advertised OS/architecture.
2. Equivalent fixture renders and migration tests.
3. Real desktop tests: docking, focus, restore, scaling, drag/drop, accessibility.
4. Hardware camera permission/shutdown tests; never activate devices in fixture tests.
5. Real player tests, unsupported commands and stale sessions.
6. Resource measurements with stated machine/workload.
7. Clean install, upgrade, uninstall and permission testing.

Hosted CI cannot prove webcam, compositor, graphical-session or notarization behavior. No platform badges/downloads until this gate is met. Windows remains the only released platform.

## Additional CI evidence (development branch)

The desktop now pins layout to a selected monitor rather than reselecting it while
an expanded panel resizes. Display-change recovery, physical-coordinate dragging,
DPI-aware snapping and compact-screen overflow are covered by synthetic geometry
and actual headless window-layout checks. Physical display hot-plug, mixed-DPI
compositors, Spaces and Wayland remain open.

Linux X11 drag/drop is exercised across two independent processes under Xvfb,
including file/photo import, shelf export, Unicode file URLs and original preservation.
The macOS drag harness requires existing CI event permission; warnings/skips must
not be counted as native drag validation. Finder, file managers and arbitrary
third-party destination applications remain distinct compatibility checks.

Development packaging now includes Debian packages, portable Linux archives, macOS
DMGs and PKGs. CI installs/reinstalls/removes its own packages and runs the synthetic
app checks from installed and mounted artifacts. This verifies packaging mechanics,
not Gatekeeper trust: Developer ID signing, notarization and real clean-user startup
remain release gates. There is still no release/upload step for these binaries.
