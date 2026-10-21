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
    let modifiers: [(Int, CGEventFlags)] = [(kVK_Control,.maskControl),(kVK_Option,.maskAlternate),(kVK_Shift,.maskShift),(kVK_Command,.maskCommand),(kVK_Function,.maskSecondaryFn)]
    func post(_ keyCode: Int, _ down: Bool, _ state: CGEventFlags, _ modifier: Bool = false) {
        let event = CGEvent(keyboardEventSource: nil, virtualKey: CGKeyCode(keyCode), keyDown: down)!
        if modifier { event.type = .flagsChanged }
        event.flags = state; event.post(tap: .cghidEventTap); pause(0.04)
    }
    var state: CGEventFlags = []
    for (modifier,flag) in modifiers where flags.contains(flag) { state.insert(flag); post(modifier,true,state,true) }
    post(code,true,state); post(code,false,state)
    // Modifier release must be a real flagsChanged event. A key-up that retains
    // Command can otherwise contaminate subsequent pointer/drag tests.
    for (modifier,flag) in modifiers.reversed() where flags.contains(flag) { state.remove(flag); post(modifier,false,state,true) }
}
func systemKey(_ code: Int, _ modifiers: String) -> Bool {
    let process = Process(); process.executableURL = URL(fileURLWithPath:"/usr/bin/osascript")
    process.arguments = ["-e", "tell application \"System Events\" to key code \(code) using {\(modifiers)}"]
    let output = Pipe(); process.standardError = output; process.standardOutput = output
    do { try process.run() } catch { return false }
    let deadline = Date().addingTimeInterval(3)
    while process.isRunning && Date() < deadline { pause(0.05) }
    if process.isRunning { process.terminate(); return false }
    if process.terminationStatus != 0 {
        let message = String(data:output.fileHandleForReading.readDataToEndOfFile(),encoding:.utf8) ?? ""
        fputs("System Events keyboard unavailable: \(message)\n",stderr)
        return false
    }
    return true
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
    if NSWorkspace.shared.runningApplications.contains(where: { ($0.bundleIdentifier ?? "").lowercased().contains("voiceover") }) { print("UNVERIFIED: existing VoiceOver process preserved"); exit(77) }
    if enabled() { print("UNVERIFIED: VoiceOver already enabled; preserve runner state"); exit(77) }
case "start":
    guard !enabled() else { exit(77) }
    key(kVK_F5, .maskCommand); pause(1)
    for _ in 0..<100 {
        if welcomeAction("Use VoiceOver") { print("Accepted the standard VoiceOver welcome dialog"); pause(0.5) }
        if enabled() { exit(0) }; pause(0.1)
    }
    // LaunchServices fallback: use the installed Apple application, not TCC edits.
    let voiceOverURL = URL(fileURLWithPath: "/System/Library/CoreServices/VoiceOver.app")
    if FileManager.default.fileExists(atPath: voiceOverURL.path) {
        NSWorkspace.shared.openApplication(at: voiceOverURL, configuration: NSWorkspace.OpenConfiguration()) { _, error in
            if let error = error { print("VoiceOver launch error:", error.localizedDescription) }
        }
        for _ in 0..<100 {
            _ = welcomeAction("Use VoiceOver")
            if enabled() { print("VoiceOver enabled through LaunchServices"); exit(0) }
            pause(0.1)
        }
    }
    print("VoiceOver processes:", NSWorkspace.shared.runningApplications.filter { ($0.bundleIdentifier ?? "").lowercased().contains("voiceover") }.map { $0.bundleIdentifier ?? "" })
    print("Modifier flags after shortcut:", CGEventSource.flagsState(.combinedSessionState).rawValue)
    print("UNVERIFIED: runner did not enable VoiceOver through its standard shortcut"); exit(77)
case "stop":
    if enabled() { _ = systemKey(kVK_F5,"command down"); pause(1) }
    for app in NSWorkspace.shared.runningApplications where app.bundleURL?.path == "/System/Library/CoreServices/VoiceOver.app" {
        _ = app.terminate()
        pause(1)
        if !app.isTerminated { _ = app.forceTerminate() }
    }
    for _ in 0..<50 {
        if !enabled() { NSPasteboard.general.clearContents(); exit(0) }; pause(0.1)
    }
    fputs("VoiceOver teardown failed\n", stderr); exit(1)
case "read-focused":
    guard enabled() else { exit(3) }
    // Inject the F4 virtual key directly (Fn is only needed on a physical media-key keyboard).
    // VO-F4 describes keyboard focus; VO-Shift-C copies the actual last phrase.
    guard systemKey(kVK_F4,"control down, option down") else { exit(77) }; pause(1.5)
    let board = NSPasteboard.general; board.clearContents(); let old = board.changeCount
    guard systemKey(kVK_ANSI_C,"control down, option down, shift down") else { exit(77) }
    for _ in 0..<30 {
        if board.changeCount != old, let phrase = board.string(forType: .string) { print(phrase); exit(0) }
        pause(0.1)
    }
    exit(3)
default: exit(2)
}
