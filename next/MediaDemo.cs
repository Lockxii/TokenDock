using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace TokenDock;

// Deterministic product renders. Never opens a window, camera, clipboard or media session.
static class MediaDemo {
 public static void Run(string directory){
  Directory.CreateDirectory(directory);string frames=Path.Combine(directory,"frames");Directory.CreateDirectory(frames);
  var prefs=new Preferences{Music=true,ClipboardHistory=true,Camera=true,LayoutOrder=["claude","codex","opencode","music","clipboard","camera","cat"]};
  var dock=new NotchSurface{Running=false};dock.SetFixture();dock.Configure(prefs);dock.SetEdge("top");
  var cover=Artwork();using var music=new MusicModule(Dispatcher.CurrentDispatcher);music.SetFixture(cover,new MusicTimeline(TimeSpan.Zero,TimeSpan.FromSeconds(240),TimeSpan.FromSeconds(72),DateTimeOffset.UtcNow,1,false,TimeSpan.Zero,TimeSpan.FromSeconds(240)));
  using var copies=new ClipboardModule();copies.History.Add("Your next idea starts here.");copies.History.Add(CopyItem.FromText("Design review · Friday, 10:00"));copies.AddPhoto(Path.Combine(directory,"demo-photo.png"),CameraPane.RoundPhoto(cover));
  Save(CameraPane.RoundPhoto(cover),Path.Combine(directory,"demo-photo.png"));
  FrameworkElement Module(string kind){if(kind=="music")return new InlineModuleView(music,null);if(kind=="clipboard")return new InlineModuleView(null,copies,_=>{});var camera=new CameraPane();camera.SetDemoImage(cover);return camera;}
  dock.Open=0;Render(dock,"Everything, one glance.","A native Windows dock. Made to stay out of your way.",Path.Combine(directory,"hero.png"));
  foreach(var kind in new[]{"music","clipboard","camera","ram"}){
   dock.SetEdge("top");dock.Open=1;if(kind=="ram"){dock.CloseModule();var cat=dock.ItemBounds("cat");dock.SelectHover(new Point(cat.X+cat.Width/2,cat.Y+cat.Height/2));}else dock.ShowModule(kind,Module(kind));
   Render(dock,kind switch{"music"=>"Keep the music close.","clipboard"=>"Copy. Keep. Drop.","camera"=>"A photo, one gesture away.",_=>"Meet your memory monitor."},"Actual interface · fictional demo data",Path.Combine(directory,kind+".png"));
  }
  var editor=new PersonalizationView(prefs,_=>{},()=>{},()=>[]);editor.Measure(new Size(492,double.PositiveInfinity));editor.Arrange(new Rect(0,0,492,editor.DesiredSize.Height));editor.UpdateLayout();var ed=new RenderTargetBitmap(492,(int)Math.Ceiling(editor.ActualHeight),96,96,PixelFormats.Pbgra32);ed.Render(editor);Save(ed,Path.Combine(directory,"customize.png"));
  dock.CloseModule();dock.SetEdge("right");dock.Open=0;int index=0;
  foreach(var kind in new[]{"ram","music","clipboard","camera","clipboard"}){
   double previousHeight=dock.BodyHeight;FrameworkElement? view=null;
   if(kind=="ram"){dock.CloseModule();var cat=dock.ItemBounds("cat");dock.SelectHover(new Point(cat.X+cat.Width/2,cat.Y+cat.Height/2));}else{view=Module(kind);dock.ShowModule(kind,view);}
   double target=dock.BodyHeight;
   for(int f=0;f<30;f++){
    double t=Math.Min(1,f/12d),ease=1-Math.Pow(1-t,3);dock.BodyHeight=previousHeight+(target-previousHeight)*ease;dock.Open=index<30?ease:1;if(view!=null)view.Opacity=.35+.65*ease;
    Render(dock,"One dock. A smoother flow.",kind switch{"ram"=>"Memory at a glance","music"=>"Music, without switching windows","clipboard"=>"Your files and ideas, ready to drag","camera"=>"Private by design: off when closed",_=>""},Path.Combine(frames,$"{index++:D4}.png"));
   }
  }
  dock.CloseModule();
 }
 static BitmapSource Artwork(){var v=new DrawingVisual();using(var d=v.RenderOpen()){d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(25,34,58),Color.FromRgb(84,65,100),45),null,new Rect(0,0,240,240));d.DrawEllipse(new SolidColorBrush(Color.FromRgb(244,193,145)),null,new Point(150,85),45,45);for(int i=0;i<12;i++)d.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(100,145,215,197)),2),new Point(0,145+i*8),new Point(240,115+i*8));}var b=new RenderTargetBitmap(240,240,96,96,PixelFormats.Pbgra32);b.Render(v);b.Freeze();return b;}
 static void Render(NotchSurface dock,string title,string caption,string path){
  dock.Measure(new Size(dock.Width,dock.Height));dock.Arrange(new Rect(0,0,dock.Width,dock.Height));dock.UpdateLayout();var pixels=new RenderTargetBitmap((int)dock.Width*2,(int)dock.Height*2,192,192,PixelFormats.Pbgra32);pixels.Render(dock);
  var v=new DrawingVisual();using(var d=v.RenderOpen()){
   d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(16,19,29),Color.FromRgb(39,35,57),35),null,new Rect(0,0,960,600));
   Text(d,"TOKENDOCK",52,42,12,Color.FromRgb(154,221,189));Text(d,title,52,76,32,Colors.WhiteSmoke);Text(d,caption,52,124,15,Color.FromRgb(166,169,185));
   var bounds=dock.CurrentShape().Bounds;double scale=Math.Min(1.22,360/bounds.Height);double x=(960-bounds.Width*scale)/2,y=194+(350-bounds.Height*scale)/2;
   d.PushClip(new RectangleGeometry(new Rect(x,y,bounds.Width*scale,bounds.Height*scale)));d.DrawImage(pixels,new Rect(x-bounds.X*scale,y-bounds.Y*scale,dock.Width*scale,dock.Height*scale));d.Pop();
   Text(d,"OFFSCREEN DEMO  /  SAMPLE DATA  /  WINDOWS",52,562,10,Color.FromRgb(127,132,152));
  }var result=new RenderTargetBitmap(960,600,96,96,PixelFormats.Pbgra32);result.Render(v);Save(result,path);
 }
 static void Text(DrawingContext d,string value,double x,double y,double size,Color color)=>d.DrawText(new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,new SolidColorBrush(color),1),new Point(x,y));
 static void Save(BitmapSource image,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);}
}
