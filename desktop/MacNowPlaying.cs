using System.Globalization;
using System.Text.Json;
namespace TokenDock;

// The bundled adapter reads the system's selected Now Playing session, including
// browsers and third-party players. No application is launched to discover media.
static class MacNowPlaying {
 internal const int MaximumJson=6*1024*1024;
 static string Native=>Path.Combine(AppFiles.Resources,"native");
 internal static bool Available=>File.Exists(Path.Combine(Native,"mediaremote-adapter.pl"))&&Directory.Exists(AppFiles.MediaFramework);
 internal static Task<string> Run(IEnumerable<string> arguments,CancellationToken token)=>ProcessRunner.Run("/usr/bin/perl",new[]{Path.Combine(Native,"mediaremote-adapter.pl"),AppFiles.MediaFramework}.Concat(arguments),token,MaximumJson);
 internal static string[] Control(string command,double position)=>command switch {
  "play-pause"=>["send","2"],"next"=>["send","4"],"previous"=>["send","5"],
  "position" when double.IsFinite(position)=>["seek",((long)(Math.Clamp(position,0,604800)*1_000_000)).ToString(CultureInfo.InvariantCulture)],
  _=>throw new IOException("Unsupported media control.")
 };
 internal static (Track Track,byte[]? Artwork) Parse(string json){
  if(json.Length>MaximumJson)throw new IOException("Media metadata exceeded its limit.");
  try{
   using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=16});var root=doc.RootElement;
   if(root.ValueKind==JsonValueKind.Null)return (Track.Empty,null);
   if(root.ValueKind!=JsonValueKind.Object)throw new IOException("Invalid Now Playing metadata.");
   string Text(string key,int limit=4096)=>root.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String?(p.GetString()??"")[..Math.Min(p.GetString()!.Length,limit)]:"";
   double Number(string key)=>root.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.Number&&p.TryGetDouble(out double n)&&double.IsFinite(n)?Math.Clamp(n,0,604800):0;
   string player=Text("bundleIdentifier",512),title=Text("title");
   if(player.Length==0||title.Length==0)return (Track.Empty,null);
   double duration=Number("duration"),position=Number("elapsedTimeNow");
   if(!root.TryGetProperty("elapsedTimeNow",out _))position=Number("elapsedTime");
   bool playing=root.TryGetProperty("playing",out var state)&&state.ValueKind==JsonValueKind.True;
   byte[]? art=null;
   if(root.TryGetProperty("artworkData",out var data)&&data.ValueKind==JsonValueKind.String){
    var encoded=data.GetString()!;
    if(encoded.Length<=5_592_408){try{var bytes=Convert.FromBase64String(encoded);if(bytes.Length<=4*1024*1024&&ImageBytes.Valid(bytes))art=bytes;}catch(FormatException){}}
   }
   return (new Track(player,title,Text("artist"),"",duration,Math.Min(duration,position),playing),art);
  }catch(JsonException e){throw new IOException("Invalid Now Playing JSON.",e);}
 }
}
