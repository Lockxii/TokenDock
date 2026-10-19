on run argv
 set action to item 1 of argv
 set targetPosition to item 2 of argv
 set artPath to item 3 of argv
 set sep to ASCII character 31
 tell application "Music"
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
   set art to ""
   try
    set picture to raw data of artwork 1 of currentSong
    set handle to open for access POSIX file artPath with write permission
    try
     set eof handle to 0
     write picture to handle
     close access handle
     set art to artPath
    on error
     close access handle
    end try
   end try
   return "Music" & sep & (name of currentSong) & sep & (artist of currentSong) & sep & art & sep & (duration of currentSong) & sep & player position & sep & (player state as text)
  end tell
end run
