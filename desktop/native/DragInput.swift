// Event injection is restricted to disposable CI. Never requests TCC permission.
import Foundation
import CoreGraphics
guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated" else { exit(2) }
let args = Array(CommandLine.arguments.dropFirst())
guard let action = args.first else { exit(2) }
if action == "preflight" { exit(CGPreflightPostEventAccess() ? 0 : 77) }
guard CGPreflightPostEventAccess() else { exit(77) }
if action == "bounds", args.count == 2, let pid = Int(args[1]) {
    let windows = CGWindowListCopyWindowInfo(.optionAll, kCGNullWindowID) as? [[String: Any]] ?? []
    for window in windows where window[kCGWindowOwnerPID as String] as? Int == pid && window[kCGWindowLayer as String] as? Int == 0 {
        if let bounds = window[kCGWindowBounds as String] as? [String: Any], let rect = CGRect(dictionaryRepresentation: bounds as CFDictionary), rect.width > 100 {
            print("\(Int(rect.minX)) \(Int(rect.minY))")
            exit(0)
        }
    }
    exit(1)
}
if action == "escape" {
    CGEvent(keyboardEventSource: nil, virtualKey: 53, keyDown: true)?.post(tap: .cghidEventTap)
    CGEvent(keyboardEventSource: nil, virtualKey: 53, keyDown: false)?.post(tap: .cghidEventTap)
    exit(0)
}
guard args.count == 3, let x = Double(args[1]), let y = Double(args[2]) else { exit(2) }
let type: CGEventType
switch action {
case "move": type = .mouseMoved
case "drag": type = .leftMouseDragged
case "down": type = .leftMouseDown
case "up": type = .leftMouseUp
default: exit(2)
}
CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: CGPoint(x: x, y: y), mouseButton: .left)?.post(tap: .cghidEventTap)
