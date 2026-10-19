using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace TokenDock;

static class ModuleIcons {
 static readonly Dictionary<string,BitmapSource> cache=new();
 public static BitmapSource Get(string name){if(cache.TryGetValue(name,out var existing))return existing;if(name=="camera"){var visual=new DrawingVisual();using(var d=visual.RenderOpen()){d.DrawRoundedRectangle(Brushes.WhiteSmoke,null,new Rect(6,17,84,62),12,12);d.DrawRoundedRectangle(Brushes.WhiteSmoke,null,new Rect(28,8,40,20),6,6);d.DrawEllipse(new SolidColorBrush(Color.FromRgb(25,28,34)),null,new Point(48,47),22,22);d.DrawEllipse(null,new Pen(Brushes.WhiteSmoke,4),new Point(48,47),13,13);}var bitmap=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return cache[name]=bitmap;}var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=96;image.UriSource=new Uri(Path.Combine(AppContext.BaseDirectory,"assets",$"module-{name}-v15.png"));image.EndInit();image.Freeze();return cache[name]=image;}
 public static Image Image(string name,double size)=>new(){Source=Get(name),Width=size,Height=size,Stretch=Stretch.Uniform};
 public static Image Transport(string name,double size=16){
  string path=name switch{"pause"=>"M3,2 H7 V18 H3 Z M12,2 H16 V18 H12 Z","previous"=>"M2,3 H5 V17 H2 Z M17,3 L6,10 17,17 Z","next"=>"M15,3 H18 V17 H15 Z M3,3 L14,10 3,17 Z",_=>"M5,2 L18,10 5,18 Z"};
  var drawing=new DrawingGroup();drawing.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,20,20))));drawing.Children.Add(new GeometryDrawing(Brushes.WhiteSmoke,null,Geometry.Parse(path)));drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();return new Image{Source=image,Width=size,Height=size};
 }
}
