// Disposable CI session only. Publishes metadata and controls; never plays audio.
import AppKit
import MediaPlayer
import Foundation
guard ProcessInfo.processInfo.environment["CI"] == "true",
      ProcessInfo.processInfo.environment["TOKENDOCK_TEST_DISPLAY"] == "isolated" else { exit(2) }
let app = NSApplication.shared
app.setActivationPolicy(.prohibited)
let center = MPNowPlayingInfoCenter.default()
let controls = MPRemoteCommandCenter.shared()
var playing = true
var position: Double = 12
var track = 1
let picture = NSImage(size: NSSize(width: 32, height: 32))
picture.lockFocus()
NSColor.systemGreen.setFill()
NSBezierPath(rect: NSRect(x: 0, y: 0, width: 32, height: 32)).fill()
picture.unlockFocus()
let artwork = MPMediaItemArtwork(boundsSize: picture.size) { _ in picture }
func publish() {
    FileHandle.standardError.write(Data("fixture: playing=\(playing), position=\(position), track=\(track)\n".utf8))
    center.nowPlayingInfo = [MPMediaItemPropertyTitle: track == 1 ? "Synthetic track" : "Synthetic next",
        MPMediaItemPropertyArtist: "TokenDock fixture", MPMediaItemPropertyPlaybackDuration: 120.0,
        MPNowPlayingInfoPropertyElapsedPlaybackTime: position,
        MPNowPlayingInfoPropertyPlaybackRate: playing ? 1.0 : 0.0,
        MPNowPlayingInfoPropertyDefaultPlaybackRate: 1.0,
        MPNowPlayingInfoPropertyCurrentPlaybackDate: Date(),
        MPNowPlayingInfoPropertyMediaType: MPNowPlayingInfoMediaType.audio.rawValue,
        MPMediaItemPropertyArtwork: artwork]
    center.playbackState = playing ? .playing : .paused
}
func register(_ command: MPRemoteCommand, _ action: @escaping (MPRemoteCommandEvent)->Void) {
    command.isEnabled = true
    command.addTarget { event in
        DispatchQueue.main.async { action(event); publish() }
        return .success
    }
}
register(controls.togglePlayPauseCommand) { _ in playing.toggle() }
register(controls.playCommand) { _ in playing = true }
register(controls.pauseCommand) { _ in playing = false }
register(controls.nextTrackCommand) { _ in track = 2; position = 0 }
register(controls.previousTrackCommand) { _ in track = 1; position = 0 }
register(controls.changePlaybackPositionCommand) { event in
    if let seek = event as? MPChangePlaybackPositionCommandEvent { position = seek.positionTime }
}
publish()
RunLoop.main.run()
