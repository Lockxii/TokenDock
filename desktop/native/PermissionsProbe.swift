// Read existing authorization only. No prompt, camera device or Apple event is sent.
import AppKit
import ApplicationServices
import AVFoundation
import Carbon
import Foundation
guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated",
      CommandLine.arguments.count == 2, let pid = Int32(CommandLine.arguments[1]) else { exit(2) }
let before = AVCaptureDevice.authorizationStatus(for: .video)
let target = NSAppleEventDescriptor(processIdentifier: pid)
let automation = AEDeterminePermissionToAutomateTarget(target.aeDesc, AEEventClass(kCoreEventClass), AEEventID(kAEGetData), false)
let after = AVCaptureDevice.authorizationStatus(for: .video)
guard before == after else { fatalError("Read-only permission probe changed camera state") }
let states: [Int32: String] = [0: "authorized", -1743: "denied", -1744: "consent-required", -600: "target-unavailable"]
guard let automationState = states[automation] else { fatalError("Unexpected automation preflight status: \(automation)") }
let result: [String: Any] = ["cameraAuthorization": before.rawValue,
    "accessibilityClientAuthorized": AXIsProcessTrusted(),
    "automationToSyntheticPlayer": automationState,
    "voiceOverEnabled": NSWorkspace.shared.isVoiceOverEnabled,
    "osVersion": ProcessInfo.processInfo.operatingSystemVersionString,
    "promptsRequested": false, "devicesOpened": false,
    "voiceOverSpeechValidation": "UNVERIFIED: AX checks do not establish VoiceOver speech output"]
print(String(data: try JSONSerialization.data(withJSONObject: result, options: [.prettyPrinted, .sortedKeys]), encoding: .utf8)!)
