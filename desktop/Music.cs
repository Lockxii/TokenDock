using System.Globalization;
using Avalonia.Threading;
namespace TokenDock;

sealed record Track(string Player,string Title,string Artist,string Artwork,double Length,double Position,bool Playing) {
 public static Track Empty=new("","Nothing playing","","",0,0,false);
 public static Track Parse(string text){
  var p=text.Split('\u001f');if(p.Length!=7)throw new IOException("Invalid media metadata.");
  double Number(string s)=>double.TryParse(s.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?Math.Clamp(n,0,7*24*60*60):0;
  double length=Number(p[4]);return new(p[0],p[1],p[2],p[3],length,Math.Min(length,Number(p[5])),p[6] is "Playing" or "playing");
 }
}
sealed class Music:IDisposable {
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};readonly CancellationTokenSource stop=new();
 readonly string artworkPath=Path.Combine(Path.GetTempPath(),"tokendock-art-"+Guid.NewGuid().ToString("N")+".jpg");
 readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(5)};
 bool enabled,busy,disposed;int generation;string? artKey;string macPlayer="";DateTime sampled;
 public Track Track{get;private set;}=Track.Empty;public byte[]? Artwork{get;private set;}public string Status{get;private set;}="";
 public event Action? Changed;
 public double Position=>Math.Min(Track.Length,Track.Position+(Track.Playing?(DateTime.UtcNow-sampled).TotalSeconds:0));
 public Music(){timer.Tick+=async(_,_)=>await Refresh();}
 internal void SetFixture(byte[] artwork){Track=new("Fixture","Signal Coast","Synthetic artist","",204,45,true);Artwork=artwork;sampled=DateTime.UtcNow;}
 public void Enable(bool value){if(enabled==value)return;enabled=value;generation++;if(value){timer.Start();_=Refresh();}else{timer.Stop();Track=Track.Empty;Artwork=null;artKey=null;Changed?.Invoke();}}
 public async Task Refresh(){
  if(!enabled||busy||disposed)return;busy=true;int version=generation;
  try{
   string value;Track? nativeTrack=null;byte[]? nativeArt=null;bool systemPlayer=false;
   if(OperatingSystem.IsLinux()){
    const string format="{{playerName}}\u001f{{title}}\u001f{{artist}}\u001f{{mpris:artUrl}}\u001f{{mpris:length / 1000000}}\u001f{{position / 1000000}}\u001f{{status}}";
    value=await ProcessRunner.Run("playerctl",["metadata","--format",format],stop.Token);
   }else if(OperatingSystem.IsMacOS()){
    if(MacNowPlaying.Available){
     try{var snapshot=MacNowPlaying.Parse(await MacNowPlaying.Run(["get","--now"],stop.Token));nativeTrack=snapshot.Track;nativeArt=snapshot.Artwork;systemPlayer=true;macPlayer="system";value="";
      if(nativeTrack==Track.Empty){try{value=await Mac("status");systemPlayer=false;}catch(IOException){macPlayer="system";}}}
     catch(Exception e)when(e is IOException or OperationCanceledException){value=await Mac("status");}
    }else value=await Mac("status");
   }
   else{Status="Use the Windows edition for native media integration.";return;}
   var track=systemPlayer?nativeTrack!:Track.Parse(value);var observedAt=DateTime.UtcNow;byte[]? art=systemPlayer?nativeArt:Artwork;
   string key=track.Artwork+"|"+track.Title;
   if(!systemPlayer&&key!=artKey){art=await ReadArt(track.Artwork);}
   if(disposed||!enabled||version!=generation)return;
   Track=track;sampled=observedAt;Artwork=art;artKey=key;Status="";Changed?.Invoke();
  }catch(Exception e)when(e is IOException or System.ComponentModel.Win32Exception or OperationCanceledException or HttpRequestException){
   if(!disposed&&enabled&&version==generation){Track=Track.Empty;Artwork=null;artKey=null;Status=OperatingSystem.IsLinux()?"No MPRIS player. Install playerctl if missing.":"No Now Playing session. Start playback in a compatible player.";Changed?.Invoke();}
  }finally{busy=false;}
 }
 async Task<byte[]?> ReadArt(string location){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(5));
  try{
   Stream stream;
   if(Path.IsPathFullyQualified(location)&&File.Exists(location)){if(new FileInfo(location).Length>4*1024*1024)return null;stream=File.OpenRead(location);}
   else if(Uri.TryCreate(location,UriKind.Absolute,out var uri)&&uri.IsFile){if(new FileInfo(uri.LocalPath).Length>4*1024*1024)return null;stream=File.OpenRead(uri.LocalPath);}
   else if(Uri.TryCreate(location,UriKind.Absolute,out uri)&&uri.Scheme=="https")stream=await http.GetStreamAsync(uri,timeout.Token);
   else return null;
   await using(stream){using var bytes=new MemoryStream();byte[] buffer=new byte[16384];int n;while((n=await stream.ReadAsync(buffer,timeout.Token))>0){if(bytes.Length+n>4*1024*1024)return null;bytes.Write(buffer,0,n);}return ImageBytes.Valid(bytes.ToArray())?bytes.ToArray():null;}
  }catch(Exception e)when(e is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException){return null;}
 }
 async Task<string> Mac(string command,string? value=null){
  if(command=="status"){
   // Inspect running bundles without launching missing apps or compiling their dictionaries.
   const string script="ObjC.import('AppKit'); $.NSWorkspace.sharedWorkspace.runningApplications.js.map(x => x.bundleIdentifier.js).filter(x => x === 'com.spotify.client' || x === 'com.apple.Music').join('\\n')";
   var running=await ProcessRunner.Run("/usr/bin/osascript",["-l","JavaScript","-e",script],stop.Token);
   Track? fallback=null;string? result=null;
   foreach(var player in new[]{"spotify","apple"}){
    if(!running.Contains(player=="spotify"?"com.spotify.client":"com.apple.Music",StringComparison.Ordinal))continue;
    try{var raw=await ReadMacPlayer(player,command,value);var track=Track.Parse(raw);if(track.Playing){macPlayer=player;return raw;}if(fallback==null){fallback=track;result=raw;macPlayer=player;}}catch(IOException){}
   }
   return result??throw new IOException("No supported player is running.");
  }
  if(macPlayer.Length==0)throw new IOException("No selected player.");
  return await ReadMacPlayer(macPlayer,command,value);
 }
 Task<string> ReadMacPlayer(string player,string command,string? value)=>ProcessRunner.Run("/usr/bin/osascript",[Path.Combine(AppContext.BaseDirectory,"native",$"music-{player}.applescript"),command,value??"",artworkPath],stop.Token);
 public async Task Command(string command,double value=0){
  if(!enabled||command is not("play-pause" or "previous" or "next" or "position"))return;
  try{
   string number=Math.Clamp(double.IsFinite(value)?value:0,0,Track.Length).ToString("0.###",CultureInfo.InvariantCulture);
   if(OperatingSystem.IsLinux())await ProcessRunner.Run("playerctl",command=="position"?["--player",Track.Player,command,number]:["--player",Track.Player,command],stop.Token);
   else if(OperatingSystem.IsMacOS()){
    if(macPlayer=="system")await MacNowPlaying.Run(MacNowPlaying.Control(command,double.Parse(number,CultureInfo.InvariantCulture)),stop.Token);
    else await Mac(command,number);
   }
   await Refresh();
  }catch(Exception e)when(e is IOException or System.ComponentModel.Win32Exception or OperationCanceledException){Status="The player did not accept this control.";Changed?.Invoke();}
 }
 public void Dispose(){disposed=true;timer.Stop();stop.Cancel();http.Dispose();try{File.Delete(artworkPath);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
static class ImageBytes {
 public static bool Valid(byte[] bytes){using var data=SkiaSharp.SKData.CreateCopy(bytes);using var codec=SkiaSharp.SKCodec.Create(data);return codec!=null&&codec.Info.Width>0&&codec.Info.Height>0&&(long)codec.Info.Width*codec.Info.Height<=4*1024*1024;}
}
