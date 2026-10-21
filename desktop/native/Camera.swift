import Foundation
import AVFoundation
import CoreVideo
import AppKit

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data((message + "\n").utf8)); exit(1)
}
func packet(_ width: Int, _ height: Int, _ pixels: Data) {
    let header = [UInt32(width).littleEndian, UInt32(height).littleEndian, UInt32(pixels.count).littleEndian]
    header.withUnsafeBytes { FileHandle.standardOutput.write(Data($0)) }
    FileHandle.standardOutput.write(pixels)
}
if CommandLine.arguments.dropFirst().first == "--self-test" {
    packet(2, 2, Data([0,0,255,255,0,255,0,255,255,0,0,255,255,255,255,255])); exit(0)
}
func permissionError(_ status: AVAuthorizationStatus) -> String? {
    switch status {
    case .denied: return "Camera access denied. Enable TokenDock in System Settings > Privacy & Security > Camera."
    case .restricted: return "Camera access is restricted by system policy. Contact your device administrator."
    default: return nil
    }
}
let mode = CommandLine.arguments.dropFirst().first
if mode == "--self-test-denied" { fail(permissionError(.denied)!) }
if mode == "--self-test-unavailable" { fail("No camera is connected.") }
if mode == "--permission-status" {
    let status = AVCaptureDevice.authorizationStatus(for: .video)
    print([AVAuthorizationStatus.notDetermined: "not-determined", .authorized: "authorized", .denied: "denied", .restricted: "restricted"][status] ?? "unknown")
    exit(0) // Read-only: never requests access, enumerates devices or creates a session.
}
// Consent-only CI mode ends before device enumeration/session creation.
if mode == "--permission-request-check" || mode == "--permission-status-check" {
    guard ProcessInfo.processInfo.environment["CI"] == "true",
          ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated" else { exit(2) }
    var report: URL?
    if CommandLine.arguments.count == 3 {
        let url = URL(fileURLWithPath:CommandLine.arguments[2]).standardizedFileURL
        guard url.deletingLastPathComponent().lastPathComponent.hasPrefix("tokendock-consent-"),
              FileManager.default.fileExists(atPath:url.deletingLastPathComponent().appendingPathComponent("fixture.marker").path),
              !FileManager.default.fileExists(atPath:url.path) else { exit(2) }
        report = url
        NSApplication.shared.setActivationPolicy(.accessory)
        NSApplication.shared.finishLaunching()
    }
    func emit(_ result: [String:Any]) {
        let data = try! JSONSerialization.data(withJSONObject:result,options:[.sortedKeys])
        if let report = report { do { try data.write(to:report,options:.atomic) } catch { fail("Cannot write consent evidence") } }
        else { print(String(data:data,encoding:.utf8)!) }
    }
    let before = AVCaptureDevice.authorizationStatus(for: .video)
    if mode == "--permission-status-check" { emit(["after":before.rawValue,"devicesOpened":false]); exit(0) }
    let permission = DispatchSemaphore(value: 0)
    var allowed = false
    AVCaptureDevice.requestAccess(for: .video) { value in allowed = value; permission.signal() }
    guard permission.wait(timeout: .now() + 25) == .success else {
        emit(["status":"UNVERIFIED","reason":"Camera consent needs an interactive response","devicesOpened":false]); exit(77)
    }
    let after = AVCaptureDevice.authorizationStatus(for: .video)
    guard allowed == (after == .authorized), after != .notDetermined else { fail("Inconsistent camera authorization response") }
    let result: [String: Any] = ["before": before.rawValue, "after": after.rawValue,
        "granted": allowed, "devicesOpened": false, "requestCompleted": true]
    emit(result)
    exit(0)
}
guard CommandLine.arguments.count == 2, mode == "--capture" else { exit(2) }
let status = AVCaptureDevice.authorizationStatus(for: .video)
if let error = permissionError(status) { fail(error) }
if status != .authorized {
    let permission = DispatchSemaphore(value: 0)
    var allowed = false
    AVCaptureDevice.requestAccess(for: .video) { value in allowed = value; permission.signal() }
    permission.wait()
    guard allowed else { fail(permissionError(.denied)!) }
}
guard let device = AVCaptureDevice.default(for: .video) else { fail("No camera is connected.") }
let session = AVCaptureSession()
session.beginConfiguration()
session.sessionPreset = .vga640x480
do {
    let input = try AVCaptureDeviceInput(device: device)
    guard session.canAddInput(input) else { fail("Camera is unavailable.") }
    session.addInput(input)
    try device.lockForConfiguration()
    if device.activeFormat.videoSupportedFrameRateRanges.contains(where: { $0.minFrameRate <= 15 && $0.maxFrameRate >= 15 }) {
        device.activeVideoMinFrameDuration = CMTime(value: 1, timescale: 15)
        device.activeVideoMaxFrameDuration = CMTime(value: 1, timescale: 15)
    }
    device.unlockForConfiguration()
} catch { fail("Camera could not start: \(error.localizedDescription)") }
final class Frames: NSObject, AVCaptureVideoDataOutputSampleBufferDelegate {
    func captureOutput(_ output: AVCaptureOutput, didOutput sample: CMSampleBuffer, from connection: AVCaptureConnection) {
        guard let buffer = CMSampleBufferGetImageBuffer(sample) else { return }
        CVPixelBufferLockBaseAddress(buffer, .readOnly)
        defer { CVPixelBufferUnlockBaseAddress(buffer, .readOnly) }
        let width = CVPixelBufferGetWidth(buffer), height = CVPixelBufferGetHeight(buffer)
        guard width > 0, height > 0, width <= 1920, height <= 1080,
              let base = CVPixelBufferGetBaseAddress(buffer) else { return }
        let stride = CVPixelBufferGetBytesPerRow(buffer)
        var data = Data(capacity: width * height * 4)
        for y in 0..<height { data.append(base.advanced(by: y * stride).assumingMemoryBound(to: UInt8.self), count: width * 4) }
        packet(width, height, data)
    }
}
let output = AVCaptureVideoDataOutput()
output.videoSettings = [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA]
output.alwaysDiscardsLateVideoFrames = true
let frames = Frames()
output.setSampleBufferDelegate(frames, queue: DispatchQueue(label: "TokenDock.Camera"))
guard session.canAddOutput(output) else { fail("Camera output is unavailable.") }
session.addOutput(output); session.commitConfiguration()
NotificationCenter.default.addObserver(forName: AVCaptureSession.runtimeErrorNotification, object: session, queue: nil) { _ in fail("Camera disconnected or interrupted.") }
session.startRunning()
RunLoop.main.run()
