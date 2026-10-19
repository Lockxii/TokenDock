using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace TokenDock;

static class MusicChecks {
 static IEnumerable<T> Find<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Find<T>(child))yield return nested;}}
 public static void Run(Action<bool> check,string dir){
  var now=DateTimeOffset.UtcNow;var timeline=new MusicTimeline(TimeSpan.Zero,TimeSpan.FromSeconds(240),TimeSpan.FromSeconds(72),now,1,true,TimeSpan.Zero,TimeSpan.FromSeconds(240));
  check(timeline.At(now.AddSeconds(8)).TotalSeconds==80);check((timeline with{Playing=false}).At(now.AddSeconds(8)).TotalSeconds==72);check((timeline with{Rate=2}).At(now.AddSeconds(8)).TotalSeconds==88);check(timeline.At(now.AddHours(1))==timeline.End);check(timeline.At(now.AddSeconds(-10))==timeline.Position);
  check(timeline.SeekTicks(.5)==TimeSpan.FromSeconds(120).Ticks);check(timeline.SeekTicks(-1)==0);check(timeline.SeekTicks(3)==timeline.End.Ticks);check(timeline.SeekTicks(double.NaN)==null);check((timeline with{MinSeek=TimeSpan.FromSeconds(30),MaxSeek=TimeSpan.FromSeconds(200)}).SeekTicks(0)==TimeSpan.FromSeconds(30).Ticks);check(MusicTimeline.Empty.SeekTicks(.5)==null);check(MusicTimeline.Empty.At(now)==TimeSpan.Zero);
  check((timeline with{End=TimeSpan.MaxValue,Position=TimeSpan.MaxValue}).At(now.AddHours(1))==TimeSpan.MaxValue);check(MusicTimeline.Clock(TimeSpan.FromSeconds(72))=="1:12");check(MusicTimeline.Clock(TimeSpan.FromSeconds(3672))=="1:01:12");
  check(MusicModule.DecodeArtwork([])==null);check(MusicModule.DecodeArtwork([1,2,3,4])==null);check(MusicModule.DecodeArtwork(new byte[4*1024*1024+1])==null);
  foreach(var name in new[]{"music","clipboard"}){var icon=ModuleIcons.Get(name);check(icon.IsFrozen&&icon.PixelWidth==96);check(ReferenceEquals(icon,ModuleIcons.Get(name)));var rgba=new FormatConvertedBitmap(icon,PixelFormats.Bgra32,null,0);var pixels=new byte[rgba.PixelWidth*rgba.PixelHeight*4];rgba.CopyPixels(pixels,rgba.PixelWidth*4,0);check(Enumerable.Range(0,pixels.Length/4).Any(i=>pixels[i*4+3]==0));check(Enumerable.Range(0,pixels.Length/4).Any(i=>pixels[i*4+3]>240));}
  // Illustration used only by --check; no synthetic track or cover is used in normal execution.
  var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(12,22,57),Color.FromRgb(101,73,123),60),null,new Rect(0,0,192,192));d.DrawEllipse(new LinearGradientBrush(Color.FromRgb(244,193,145),Color.FromRgb(255,227,179),90),null,new Point(112,73),40,40);for(int i=0;i<9;i++)d.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(35+i*15),151,212,220)),2),new Point(0,113+i*8),new Point(192,96+i*8));}
  var bitmap=new RenderTargetBitmap(192,192,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var bytes=new MemoryStream();encoder.Save(bytes);File.WriteAllBytes(Path.Combine(dir,"fixture-album.png"),bytes.ToArray());var cover=MusicModule.DecodeArtwork(bytes.ToArray());check(cover!=null&&cover.IsFrozen&&cover.PixelWidth==192);
  using var music=new MusicModule(Dispatcher.CurrentDispatcher);music.SetFixture(cover,timeline);var surface=new NotchSurface{Running=false};surface.SetFixture();surface.Configure(new Preferences{Music=true,ClipboardHistory=true});
  foreach(var edge in new[]{"top","bottom","left","right"}){
   surface.SetEdge(edge);surface.ShowModule("music",new InlineModuleView(music,null));surface.RenderFile(Path.Combine(dir,$"music-artwork-{edge}.png"),true,edge);var pane=Find<MusicPane>(surface).Single();var slider=Find<Slider>(pane).Single();check(slider.IsEnabled&&slider.Value>.29&&slider.Value<.40);check(Find<Image>(pane).Any(image=>ReferenceEquals(image.Source,cover)));check(!pane.TimerRunning);
   check(Find<TextBlock>(pane).Any(t=>t.Text=="4:00"));check(Find<Button>(pane).All(b=>b.Content is Image&&b.IsEnabled));
   foreach(double open in new[]{0d,.5,1d}){surface.Open=open;foreach(var id in new[]{"music","clipboard"}){var r=surface.ModuleRect(id);check(new[]{r.TopLeft,r.TopRight,r.BottomLeft,r.BottomRight}.All(surface.Contains));check(surface.ActionAt(new Point(r.X+r.Width/2,r.Y+r.Height/2))==id);}if(edge is "left" or "right")foreach(var rect in surface.VerticalRailContentBounds())check(new[]{rect.TopLeft,rect.TopRight,rect.BottomLeft,rect.BottomRight}.All(surface.Contains));}
   surface.CloseModule();check(!pane.TimerRunning);
  }
  surface.SetEdge("top");surface.ShowModule("music",new InlineModuleView(music,null));music.SetFixture(cover,timeline with{Playing=false,Position=TimeSpan.FromSeconds(130)});surface.RenderFile(Path.Combine(dir,"music-paused.png"),true,"top");check(Find<TextBlock>(surface).Any(t=>t.Text=="2:10"));
  music.SetFixture(null,MusicTimeline.Empty);surface.RenderFile(Path.Combine(dir,"music-no-duration.png"),true,"top");check(!Find<Slider>(surface).Single().IsEnabled);check(Find<TextBlock>(surface).Any(t=>t.Text=="—:—"));surface.CloseModule();
 }
}
