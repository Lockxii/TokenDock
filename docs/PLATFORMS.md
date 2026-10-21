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

## Native accessibility and permission checks

CI launches an isolated synthetic dock and reads its accessibility tree from another process: Linux uses AT-SPI (the interface used by Orca), and macOS uses NSAccessibility/AX (used by VoiceOver). The probes check names/roles in music, Copies, camera and settings and invoke the rail's Camera action through the OS accessibility API. This opens a synthetic frame only. macOS preflights existing AX client permission without displaying a prompt or modifying TCC; denial is explicitly **unverified**, never a pass. This validates the bridge, not spoken output or complete usability with Orca/VoiceOver.

Camera error fixtures return denial and device-unavailable failures through the real child-process protocol before any device API runs. Tests verify the error reaches the UI and no stale frame survives. The Mac helper can report its existing authorization status without requesting access; denied and policy-restricted states have distinct guidance. Real TCC prompts/revocation and physical device interruption remain unverified. Synthetic unreadable-file tests also verify that denied shelf imports preserve the original.

The external macOS probe exposed missing action advertisement in Avalonia 12.1.4 even though AXPress could activate the button. The pinned native backend patch in `desktop/native/avalonia` explicitly advertises supported actions. CI requires AXPress on Camera and checks the installed hardened-runtime bundle as well as the framework-dependent build. The Linux probe retries only stale-object errors during panel replacement; persistent errors still fail.

The AppleScript music fallback preserves automation-denied error `-1743` and shows the Automation settings path instead of claiming no player exists. A synthetic `osascript` error verifies this process/error boundary without sending Apple Events or opening a player. Actual first-use consent and revocation remain separate validation gates.

## Reader output and real permission boundaries

The Linux CI reader test runs the installed Orca in an isolated D-Bus/Xvfb session with a null audio sink. It checks Orca's own `SPEECH OUTPUT` diagnostics after native focus changes; a small harness flushes the diagnostic stream without changing speech generation. This tests generated announcements, not perceived audio quality or a complete human navigation audit.

The macOS reader harness attempts Command-F5 and then launches the installed Apple VoiceOver application through LaunchServices. It uses System Events to send the F4 virtual key with VO modifiers and VO-Shift-C to copy the actual last phrase. Function-key injection does not need the physical Fn modifier. It requires existing AX/event permission, preserves an already-enabled reader by declining the test, and disables the reader it started in cleanup. Missing permission or inability to enable the reader is explicitly UNVERIFIED, never proof of compatibility. No TCC database or scripting-permission preference is modified. The copied data belongs to the disposable CI session.

A read-only macOS probe reports actual camera, AX and Apple Events authorization for the synthetic player without requesting consent, sending an Apple event or opening a device. A separate OS sandbox rule denies reading one synthetic file while the real file shelf tries importing it, and verifies the original remains intact. That proves an OS-enforced file-access denial, not camera/Automation TCC prompt or revocation behavior. The permission snapshot is retained as CI evidence. Physical camera access, first-use consent, revocation and full VoiceOver/Orca usability remain release gates.

References: [Orca diagnostic options](https://teams.pages.gitlab.gnome.org/websites/help.gnome.org/orca/introduction.html), [VoiceOver orientation commands](https://support.apple.com/en-ca/guide/voiceover/cpvokys03/10/mac/27), [VoiceOver last phrase](https://support.apple.com/en-ca/guide/voiceover/vo2725/mac).

Orca also exposed decorative layout class names being spoken as context. Application styles now suppress fallback class labels on layout containers while preserving their children and semantic controls; the reader probe rejects those implementation labels in generated speech.

### Camera consent without camera hardware

The guarded `--permission-request-check` helper mode calls the real AVFoundation authorization API and exits **before** enumerating devices or constructing a capture session. The disposable CI harness denies only a camera dialog naming TokenDock or the observed `hosted-compute-agent` responsible process. It checks the callback against the OS authorization status and reads that status again from a second process. `artifacts/camera-consent.json` records whether a real dialog was denied and which identity appeared. An inaccessible or unanswered prompt is explicitly UNVERIFIED.

On hosted runners, command-line helper requests can be attributed to the parent `hosted-compute-agent`. Such a result exercises the real helper denial path, but does **not** prove consent attribution for a Developer ID-signed installed TokenDock bundle. No camera access is granted by the test; no TCC database is edited. Grant/revoke behavior for the installed app and actual camera interruption remain release gates.
