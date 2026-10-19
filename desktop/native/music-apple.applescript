on run argv
 set tdAction to item 1 of argv
 set tdPosition to item 2 of argv
 set tdArtPath to item 3 of argv
 set tdSeparator to ASCII character 31
 set tdArtworkBytes to missing value
 tell application "Music"
  if tdAction is "play-pause" then
   playpause
  else if tdAction is "next" then
   next track
  else if tdAction is "previous" then
   previous track
  else if tdAction is "position" then
   set player position to tdPosition as real
  end if
  set tdTrack to current track
  set tdTitle to name of tdTrack
  set tdArtist to artist of tdTrack
  set tdLength to duration of tdTrack
  set tdCurrentPosition to player position
  set tdState to player state as text
  try
   set tdArtworkBytes to raw data of artwork 1 of tdTrack
  end try
 end tell
 set tdExportedArt to ""
 if tdArtworkBytes is not missing value then
  set tdHandle to open for access POSIX file tdArtPath with write permission
  try
   set eof tdHandle to 0
   write tdArtworkBytes to tdHandle
   close access tdHandle
   set tdExportedArt to tdArtPath
  on error
   close access tdHandle
  end try
 end if
 return "Music" & tdSeparator & tdTitle & tdSeparator & tdArtist & tdSeparator & tdExportedArt & tdSeparator & tdLength & tdSeparator & tdCurrentPosition & tdSeparator & tdState
end run
