using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using SkiaSharp;
namespace TokenDock;
static class DesktopChecks {
 public static int Run(string[] args){
  int checks=0;void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);checks++;}
  string directory=args.Length>1?args[1]:Path.Combine(Path.GetTempPath(),"tokendock-render-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  var spring=new Spring(0){Target=400};for(int i=0;i<8;i++)spring.Step(.016);double before=spring.Value,velocity=spring.Velocity;spring.Target=150;Check(spring.Value==before&&spring.Velocity==velocity,"Retarget preserves motion");for(int i=0;i<100;i++)spring.Step(.016);Check(spring.Value==150,"Interrupted transition settles");
  var area=new PixelRect(-1920,0,1920,1080);foreach(var edge in Enum.GetValues<Edge>()){var position=DockPlacement.Locate(edge,area,new PixelSize(400,200),new PixelPoint(-900,300));Check(area.Contains(position),"Placement on negative-coordinate monitor");}
  var pixels=Enumerable.Repeat((byte)255,64*64*4).ToArray();var frame=new CameraFrame(64,64,pixels);var png=frame.RoundPng();Check(CopyItem.FromPng(png)!=null,"Circular photo exports PNG");
  using(var decoded=SKBitmap.Decode(png)){Check(decoded.GetPixel(0,0).Alpha==0,"Photo corners remain transparent");Check(decoded.GetPixel(32,32).Alpha==255,"Photo center remains opaque");}
  foreach(var values in new[]{new[]{-1,10,40},new[]{4000,4000,64000000},new[]{20,20,1}}){byte[] header=new byte[12];for(int i=0;i<3;i++)System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(i*4,4),values[i]);bool rejected=false;try{CameraFrame.Header(header);}catch(IOException){rejected=true;}Check(rejected,"Reject invalid camera packet");}
  string temp=Path.Combine(Path.GetTempPath(),"tokendock-shelf-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   string original=Path.Combine(temp,"file.txt");File.WriteAllText(original,"synthetic file shelf fixture");var shelf=new FileShelf();
   var item=Task.Run(()=>shelf.Store([original])).GetAwaiter().GetResult();Check(item.Files[0]!=original&&File.ReadAllText(item.Files[0])==File.ReadAllText(original),"Drop creates an independent file copy");
   File.WriteAllText(original,"changed original");Check(File.ReadAllText(item.Files[0])=="synthetic file shelf fixture","Shelf preserves the dropped snapshot");
   shelf.Dispose();SpinWait.SpinUntil(()=>!File.Exists(item.Files[0]),3000);Check(!File.Exists(item.Files[0])&&File.Exists(original),"Cleanup preserves originals");
  }finally{Directory.Delete(temp,true);}
  var history=new CopyHistory();for(int i=0;i<25;i++)history.Add(CopyItem.FromText("fixture "+i));Check(history.Items.Count==20,"History bound");history.Remove(history.Items[0].Key);Check(history.Items.Count==19,"Individual removal");
  Check(Track.Parse("test\u001fTitle\u001fArtist\u001f\u001f120\u001f500\u001fPlaying").Position==120,"Media timeline clamp");
  Check(Track.Parse("test\u001fTitle\u001fArtist\u001f\u001f1e300\u001f1,5\u001fPlaying").Length==604800,"Untrusted media duration is bounded");
  Check(Track.Parse("test\u001fTitle\u001fArtist\u001f\u001f120\u001f1,5\u001fPlaying").Position==1.5,"Localized player decimals");
  foreach(var player in new[]{"com.apple.Safari","com.google.Chrome","org.videolan.vlc","com.colliderli.iina","com.tidal.desktop","com.spotify.client"}){
   var parsed=MacNowPlaying.Parse(System.Text.Json.JsonSerializer.Serialize(new{bundleIdentifier=player,title="Fixture",artist="Test",duration=120,elapsedTimeNow=40,playing=true,artworkData=Convert.ToBase64String(png)}));
   Check(parsed.Track.Player==player&&parsed.Track.Position==40&&parsed.Track.Playing,"Universal macOS session metadata");Check(parsed.Artwork!=null,"Universal macOS artwork");
  }
  Check(MacNowPlaying.Parse("null").Track==Track.Empty,"Closed macOS session clears metadata");
  Check(MacNowPlaying.Parse("{\"bundleIdentifier\":\"fixture\",\"title\":\"T\",\"duration\":-1,\"elapsedTimeNow\":1e300,\"artworkData\":\"invalid!\"}").Artwork==null,"Malformed artwork cannot break metadata");
  foreach(var invalid in new[]{"[]","{","{\"x\":"+new string('[',20)+"0"+new string(']',20)+"}"}){bool rejected=false;try{MacNowPlaying.Parse(invalid);}catch(IOException){rejected=true;}Check(rejected,"Malformed Now Playing payload rejected");}
  Check(MacNowPlaying.Control("position",1.5).SequenceEqual(new[]{"seek","1500000"}),"Native seek uses microseconds");
  Check(MacNowPlaying.Control("next",0).SequenceEqual(new[]{"send","4"})&&MacNowPlaying.Control("previous",0)[1]=="5"&&MacNowPlaying.Control("play-pause",0)[1]=="2","Native transport command mapping");
  Check(Copies.Excluded(["org.nspasteboard.ConcealedType"]),"Concealed clipboard payload is excluded");
  using var dock=new DockWindow(true);dock.Show();
  foreach(var edge in Enum.GetValues<Edge>())foreach(var module in new[]{"all","codex","music","clipboard","camera","cat","settings"}){
   dock.SetEdge(edge);dock.ShowModule(module);dock.Settle();Dispatcher.UIThread.RunJobs();
   Check(dock.Selected==module&&dock.Expanded,"Module opens inline");
   AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
   using var image=dock.CaptureRenderedFrame();Check(image!=null,"Offscreen surface renders");
   image!.Save(Path.Combine(directory,$"{edge}-{module}.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());dock.Collapse();Check(!dock.Expanded,"Panel closes");
  }
  foreach(var id in Enumerable.Range(0,120).Select(i=>new[]{"camera","clipboard","music","cat"}[i%4])){dock.ShowModule(id);Dispatcher.UIThread.RunJobs();Check(dock.Selected==id,"Rapid retarget keeps latest module");}dock.Settle();
  string helper=Camera.HelperPath;
  if(File.Exists(helper)){
   using var camera=new Camera(true,helper);int frames=0;camera.Changed+=()=>{if(camera.Frame!=null)frames++;};camera.SetActive(true);
   var watch=System.Diagnostics.Stopwatch.StartNew();while(!camera.Stopped.IsCompleted&&watch.ElapsedMilliseconds<5000){Dispatcher.UIThread.RunJobs();Thread.Sleep(5);}Check(camera.Stopped.IsCompleted&&frames>0,"Native synthetic camera reaches the UI");
   for(int i=0;i<12;i++){camera.SetActive(true);camera.SetActive(false);}watch.Restart();while(!camera.Stopped.IsCompleted&&watch.ElapsedMilliseconds<5000){Dispatcher.UIThread.RunJobs();Thread.Sleep(5);}Check(camera.Stopped.IsCompleted&&camera.Frame==null,"Rapid camera cancellation drains capture work");
  }else Console.WriteLine("SKIP: native camera transport lifecycle (no helper for this host).");
  dock.Close();Console.WriteLine($"PASS: {checks} desktop checks (synthetic only; no camera, clipboard, player or desktop captured).");return 0;
 }
}
