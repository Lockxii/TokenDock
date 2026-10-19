import Foundation
import AVFoundation
import CoreVideo

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
guard CommandLine.arguments.count == 2, CommandLine.arguments[1] == "--capture" else { exit(2) }
let permission = DispatchSemaphore(value: 0)
var allowed = false
AVCaptureDevice.requestAccess(for: .video) { value in allowed = value; permission.signal() }
permission.wait()
guard allowed else { fail("Camera access denied. Enable TokenDock in System Settings > Privacy & Security > Camera.") }
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
NotificationCenter.default.addObserver(forName: .AVCaptureSessionRuntimeError, object: session, queue: nil) { _ in fail("Camera disconnected or interrupted.") }
session.startRunning()
RunLoop.main.run()
