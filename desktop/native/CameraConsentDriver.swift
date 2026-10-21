// Respond only to the synthetic TokenDock camera consent dialog on disposable CI.
import AppKit
import ApplicationServices
guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated" else { exit(2) }
guard AXIsProcessTrusted() else { exit(77) }
func attr(_ e: AXUIElement, _ key: String) -> CFTypeRef? {
    var result: CFTypeRef?
    guard AXUIElementCopyAttributeValue(e,key as CFString,&result) == .success else { return nil }
    return result
}
var seen = Set<String>()
let deadline = Date().addingTimeInterval(18)
while Date() < deadline {
    for app in NSWorkspace.shared.runningApplications where ["com.apple.UserNotificationCenter", "com.apple.SecurityAgent", "com.apple.tccd"].contains(app.bundleIdentifier ?? "") {
        let root = AXUIElementCreateApplication(app.processIdentifier)
        AXUIElementSetMessagingTimeout(root,0.2)
        for window in attr(root,kAXWindowsAttribute) as? [AXUIElement] ?? [] {
            var labels = [String](); var buttons = [(String,AXUIElement)](); var count = 0
            func visit(_ e: AXUIElement, _ depth: Int) {
                guard depth < 12, count < 150 else { return }; count += 1
                let text = [kAXTitleAttribute,kAXDescriptionAttribute,kAXValueAttribute].compactMap { attr(e,$0) as? String }.joined(separator:" ")
                if !text.isEmpty { labels.append(text) }
                if (attr(e,kAXRoleAttribute) as? String) == "AXButton" { buttons.append((text,e)) }
                for child in attr(e,kAXChildrenAttribute) as? [AXUIElement] ?? [] { visit(child,depth+1) }
            }
            visit(window,0)
            let text = labels.joined(separator:" | ")
            if seen.insert(text).inserted { print("CI consent dialog:",text) }
            // CLI children inherit the hosted runner's responsible TCC identity. Deny
            // only the two observed fixture identities; never grant camera access.
            guard (text.contains("TokenDock") || text.contains("hosted-compute-agent")), text.localizedCaseInsensitiveContains("camera") else { continue }
            for (label,button) in buttons where label.contains("Don't Allow") || label.contains("Don’t Allow") {
                guard AXUIElementPerformAction(button,kAXPressAction as CFString) == .success else { exit(1) }
                print("PASS: denied fixture camera through the real consent dialog"); exit(0)
            }
        }
    }
    RunLoop.current.run(until:Date().addingTimeInterval(0.2))
}
print("UNVERIFIED: matching TokenDock consent dialog not accessible"); exit(77)
