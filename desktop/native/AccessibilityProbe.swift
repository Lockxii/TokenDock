import ApplicationServices
import Foundation

guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated" else { exit(2) }
// Never request permission or change TCC. Hosted runners may deny this client.
guard AXIsProcessTrusted() else { print("UNVERIFIED: runner denies accessibility-client permission"); exit(77) }
if CommandLine.arguments.last == "preflight" { exit(0) }
guard CommandLine.arguments.count >= 3, let pid = Int32(CommandLine.arguments[1]) else { exit(2) }
let action = CommandLine.arguments[2]
let app = AXUIElementCreateApplication(pid)
AXUIElementSetMessagingTimeout(app, 2)
func attribute(_ element: AXUIElement, _ name: String) -> CFTypeRef? {
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else { return nil }
    return value
}
var nodes: [[String: Any]] = []
var pressed = false
func visit(_ element: AXUIElement, _ depth: Int) {
    guard depth < 32, nodes.count < 2000 else { return }
    let role = attribute(element, kAXRoleAttribute) as? String ?? ""
    let name = [attribute(element, kAXTitleAttribute) as? String, attribute(element, kAXDescriptionAttribute) as? String].compactMap { $0 }.first { !$0.isEmpty } ?? ""
    var names: CFArray?
    AXUIElementCopyActionNames(element, &names)
    let actions = names as? [String] ?? []
    nodes.append(["name": name, "role": role, "actions": actions])
    if action == "press-camera" && name == "Camera" && actions.contains(kAXPressAction) && !pressed {
        pressed = AXUIElementPerformAction(element, kAXPressAction as CFString) == .success
    }
    for child in attribute(element, kAXChildrenAttribute) as? [AXUIElement] ?? [] { visit(child, depth + 1) }
}
visit(app, 0)
if action == "press-camera" { guard pressed else { exit(3) } }
let data = try JSONSerialization.data(withJSONObject: nodes)
print(String(data: data, encoding: .utf8)!)
