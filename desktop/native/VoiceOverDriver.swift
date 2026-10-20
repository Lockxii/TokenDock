// Documented VoiceOver keyboard commands on disposable CI only. No TCC edits.
import AppKit
import ApplicationServices
import Carbon
import Foundation
guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated",
      CommandLine.arguments.count == 2 else { exit(2) }
guard AXIsProcessTrusted(), CGPreflightPostEventAccess() else { print("UNVERIFIED: existing AX/event permission unavailable"); exit(77) }
func pause(_ seconds: Double) { RunLoop.current.run(until: Date().addingTimeInterval(seconds)) }
func key(_ code: Int, _ flags: CGEventFlags) {
    for down in [true, false] {
        let event = CGEvent(keyboardEventSource: nil, virtualKey: CGKeyCode(code), keyDown: down)!
        event.flags = flags; event.post(tap: .cghidEventTap); pause(0.05)
    }
}
func enabled() -> Bool { NSWorkspace.shared.isVoiceOverEnabled }
switch CommandLine.arguments[1] {
case "preflight":
    if enabled() { print("UNVERIFIED: VoiceOver already enabled; preserve runner state"); exit(77) }
case "start":
    guard !enabled() else { exit(77) }
    key(kVK_F5, .maskCommand)
    for _ in 0..<100 { if enabled() { exit(0) }; pause(0.1) }
    print("UNVERIFIED: runner did not enable VoiceOver through its standard shortcut"); exit(77)
case "stop":
    if enabled() { key(kVK_F5, .maskCommand) }
    for _ in 0..<100 { if !enabled() { NSPasteboard.general.clearContents(); exit(0) }; pause(0.1) }
    fputs("VoiceOver teardown failed\n", stderr); exit(1)
case "read-focused":
    guard enabled() else { exit(3) }
    // VO-Fn-F4 describes keyboard focus; VO-Shift-C copies the actual last phrase.
    key(kVK_F4, [.maskControl, .maskAlternate, .maskSecondaryFn]); pause(0.7)
    let board = NSPasteboard.general; board.clearContents(); let old = board.changeCount
    key(kVK_ANSI_C, [.maskControl, .maskAlternate, .maskShift])
    for _ in 0..<30 {
        if board.changeCount != old, let phrase = board.string(forType: .string) { print(phrase); exit(0) }
        pause(0.1)
    }
    exit(3)
default: exit(2)
}
