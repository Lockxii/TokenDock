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
  var frame=new CameraFrame(2,2,[0,0,255,255,0,255,0,255,255,0,0,255,255,255,255,255]);var png=frame.RoundPng();Check(CopyItem.FromPng(png)!=null,"Circular photo exports PNG");
  var history=new CopyHistory();for(int i=0;i<25;i++)history.Add(CopyItem.FromText("fixture "+i));Check(history.Items.Count==20,"History bound");history.Remove(history.Items[0].Key);Check(history.Items.Count==19,"Individual removal");
  Check(Track.Parse("test\u001fTitle\u001fArtist\u001f\u001f120\u001f500\u001fPlaying").Position==120,"Media timeline clamp");
  using var dock=new DockWindow(true);dock.Show();
  foreach(var edge in Enum.GetValues<Edge>())foreach(var module in new[]{"all","codex","music","clipboard","camera","cat","settings"}){
   dock.SetEdge(edge);dock.ShowModule(module);dock.Settle();Dispatcher.UIThread.RunJobs();
   Check(dock.Selected==module&&dock.Expanded,"Module opens inline");
   AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
   using var image=dock.CaptureRenderedFrame();Check(image!=null,"Offscreen surface renders");
   image!.Save(Path.Combine(directory,$"{edge}-{module}.png"),new Avalonia.Media.Imaging.PngBitmapEncoderOptions());dock.Collapse();Check(!dock.Expanded,"Panel closes");
  }
  dock.Close();Console.WriteLine($"PASS: {checks} desktop checks (synthetic only; no camera, clipboard, player or desktop captured).");return 0;
 }
}
