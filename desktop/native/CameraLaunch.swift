// Launch only the CI-installed camera helper via LaunchServices, without capture.
import AppKit
import Foundation
guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated",
      CommandLine.arguments.count == 4 else { exit(2) }
let url = URL(fileURLWithPath:CommandLine.arguments[1]).standardizedFileURL
let mode = CommandLine.arguments[2]
guard url.path == "/Applications/TokenDock.app/Contents/Helpers/TokenDockCamera.app",
      Bundle(url:url)?.bundleIdentifier == "io.github.lockxii.tokendock.camera",
      ["--permission-request-check","--permission-status-check"].contains(mode) else { exit(2) }
let configuration = NSWorkspace.OpenConfiguration()
configuration.arguments = [mode,CommandLine.arguments[3]]
configuration.environment = ["CI":"true","TOKENDOCK_TEST_DISPLAY":"isolated"]
configuration.activates = false
var finished = false
var application: NSRunningApplication?
var failed = false
NSWorkspace.shared.openApplication(at:url,configuration:configuration) { app,error in
    application = app; failed = error != nil; finished = true
    if let error = error { fputs("Camera launch failed: \(error.localizedDescription)\n",stderr) }
}
let deadline = Date().addingTimeInterval(35)
while Date() < deadline {
    if failed { exit(1) }
    if finished, let app = application, app.isTerminated { exit(0) }
    RunLoop.current.run(until:Date().addingTimeInterval(0.1))
}
if let app = application { _ = app.terminate(); if !app.isTerminated { _ = app.forceTerminate() } }
fputs("Camera permission helper did not terminate\n",stderr); exit(1)
