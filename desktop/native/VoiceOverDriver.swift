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
func welcomeAction(_ title: String) -> Bool {
    for process in NSWorkspace.shared.runningApplications where (process.bundleIdentifier ?? "").lowercased().contains("voiceover") {
        let app = AXUIElementCreateApplication(process.processIdentifier)
        AXUIElementSetMessagingTimeout(app, 1)
        var count = 0
        func visit(_ element: AXUIElement, _ depth: Int) -> Bool {
            guard depth < 12, count < 300 else { return false }; count += 1
            func attribute(_ key: String) -> CFTypeRef? {
                var value: CFTypeRef?
                guard AXUIElementCopyAttributeValue(element, key as CFString, &value) == .success else { return nil }
                return value
            }
            let role = attribute(kAXRoleAttribute) as? String ?? ""
            if role == "AXMenuBar" { return false }
            if role == "AXButton", (attribute(kAXTitleAttribute) as? String) == title {
                return AXUIElementPerformAction(element, kAXPressAction as CFString) == .success
            }
            for child in attribute(kAXChildrenAttribute) as? [AXUIElement] ?? [] {
                if visit(child, depth + 1) { return true }
            }
            return false
        }
        if visit(app, 0) { return true }
    }
    return false
}
func enabled() -> Bool { NSWorkspace.shared.isVoiceOverEnabled }
switch CommandLine.arguments[1] {
case "preflight":
    if enabled() { print("UNVERIFIED: VoiceOver already enabled; preserve runner state"); exit(77) }
case "start":
    guard !enabled() else { exit(77) }
    key(kVK_F5, .maskCommand); pause(1)
    for _ in 0..<100 {
        if welcomeAction("Use VoiceOver") { print("Accepted the standard VoiceOver welcome dialog"); pause(0.5) }
        if enabled() { exit(0) }; pause(0.1)
    }
    print("UNVERIFIED: runner did not enable VoiceOver through its standard shortcut"); exit(77)
case "stop":
    _ = welcomeAction("Turn Off VoiceOver")
    if enabled() { key(kVK_F5, .maskCommand) }
    pause(0.5)
    for _ in 0..<100 {
        _ = welcomeAction("Turn Off VoiceOver")
        if !enabled() { NSPasteboard.general.clearContents(); exit(0) }; pause(0.1)
    }
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
