# Linux and macOS: full-parity release gate

**Not implemented or supported by the current Windows build.** This is a requirements inventory, not a compatibility announcement. No native port has been completed or validated.

The owner requires full parity before publication. A token-only prototype, web mockup or compatibility-layer build does not qualify.

## Dependencies to replace

WPF rendering, WinForms tray, Win32 placement, GSMTC music, WinRT MediaCapture, user32 clipboard, GlobalMemoryStatusEx and winsqlite3 are Windows-specific. Parsing, preferences and animation math contain reusable logic, but native dependencies must be separated and tested. No replacement framework is committed yet.

## Required parity — all pending

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
