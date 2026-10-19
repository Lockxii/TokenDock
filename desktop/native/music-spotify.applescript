on run argv
 set action to item 1 of argv
 set targetPosition to item 2 of argv
 set artPath to item 3 of argv
 set sep to ASCII character 31
 tell application "Spotify"
   if action is "play-pause" then
    playpause
   else if action is "next" then
    next track
   else if action is "previous" then
    previous track
   else if action is "position" then
    set player position to targetPosition as real
   end if
   set currentSong to current track
   return "Spotify" & sep & (name of currentSong) & sep & (artist of currentSong) & sep & (artwork url of currentSong) & sep & ((duration of currentSong) / 1000) & sep & player position & sep & (player state as text)
  end tell
end run
