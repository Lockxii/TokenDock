// Event injection is restricted to disposable CI. Never requests TCC permission.
import Foundation
import CoreGraphics
guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated" else { exit(2) }
let args = Array(CommandLine.arguments.dropFirst())
guard let action = args.first else { exit(2) }
if action == "preflight" { exit(CGPreflightPostEventAccess() ? 0 : 77) }
guard CGPreflightPostEventAccess() else { exit(77) }
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
let event = CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: CGPoint(x: x, y: y), mouseButton: .left)
event?.setIntegerValueField(.mouseEventClickState, value: 1)
event?.post(tap: .cghidEventTap)
