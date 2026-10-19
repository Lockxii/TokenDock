using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
namespace TokenDock;
static class Ui {
 public static readonly IBrush Mint=new SolidColorBrush(Color.Parse("#91DEBC")),Muted=new SolidColorBrush(Color.Parse("#93939F")),Card=new SolidColorBrush(Color.Parse("#1D1D23"));
 public static TextBlock Text(string text,double size=14,IBrush? color=null)=>new(){Text=text,FontSize=size,Foreground=color??Brushes.White,TextWrapping=TextWrapping.Wrap};
 public static Button Button(object label,Action click,string? accessible=null){var button=new Button{Content=label,Padding=new Thickness(12,8),CornerRadius=new CornerRadius(12),Background=Card,BorderThickness=new Thickness(0)};button.Click+=(_,_)=>click();Avalonia.Automation.AutomationProperties.SetName(button,accessible??label.ToString()??"");return button;}
 public static Control Icon(string id,double size=30){
  string? name=id switch{"codex"=>"codex.png","claude"=>"claude.png","opencode"=>"opencode.png","music"=>"module-music-v15.png","clipboard"=>"module-clipboard-v15.png",_=>null};
  if(name!=null)return new Image{Source=Assets.Get(name),Width=size,Height=size};
  if(id=="camera")return new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse("M4,8 L9,8 12,4 20,4 23,8 28,8 28,26 4,26 Z M11,17 A5,5 0 1 0 21,17 A5,5 0 1 0 11,17"),Stroke=Brushes.White,StrokeThickness=2,Width=size,Height=size,Stretch=Stretch.Uniform};
  return Text("◇",size);
 }
 public static StackPanel Stack(params Control[] controls){var stack=new StackPanel{Spacing=12};foreach(var control in controls)stack.Children.Add(control);return stack;}
 public static Control Transport(string name)=>new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(name switch{"previous"=>"M2,3 H5 V17 H2 Z M17,3 L6,10 17,17 Z","next"=>"M15,3 H18 V17 H15 Z M3,3 L14,10 3,17 Z","pause"=>"M3,2 H7 V18 H3 Z M12,2 H16 V18 H12 Z",_=>"M5,2 L18,10 5,18 Z"}),Fill=Brushes.White,Width=18,Height=18,Stretch=Stretch.Uniform};
}
static class Assets {
 static readonly Dictionary<string,Bitmap> cache=[];
 public static Bitmap Get(string name){if(!cache.TryGetValue(name,out var image))cache[name]=image=new Bitmap(Path.Combine(AppContext.BaseDirectory,"assets",name));return image;}
 public static Bitmap CatFrame(int frame,int heat){
  string key=$"cat-{frame}-{heat}";if(cache.TryGetValue(key,out var cached))return cached;
  using var source=SkiaSharp.SKBitmap.Decode(Path.Combine(AppContext.BaseDirectory,"assets","cat-run.png"));
  int a=(int)Math.Round(frame*source.Width/8d),b=(int)Math.Round((frame+1)*source.Width/8d);using var crop=new SkiaSharp.SKBitmap(b-a,200);
  using(var canvas=new SkiaSharp.SKCanvas(crop))canvas.DrawBitmap(source,new SkiaSharp.SKRect(a,270,b,470),new SkiaSharp.SKRect(0,0,crop.Width,200));
  if(heat>0){var pixels=crop.Pixels;for(int i=0;i<pixels.Length;i++){var p=pixels[i];pixels[i]=new SkiaSharp.SKColor(p.Red,(byte)(p.Green*(heat==2?.45:.75)),(byte)(p.Blue*.4),p.Alpha);}crop.Pixels=pixels;}
  using var image=SkiaSharp.SKImage.FromBitmap(crop);using var png=image.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);return cache[key]=new Bitmap(png.AsStream());
 }
}
sealed class Cat:Control {
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(66)};double phase;MemoryInfo memory;Preferences prefs;
 public Cat(Preferences prefs){this.prefs=prefs;Width=40;Height=40;timer.Tick+=(_,_)=>{if(!prefs.ReduceMotion){phase+=memory.CyclesPerSecond*prefs.CatSpeed*.066;InvalidateVisual();}};AttachedToVisualTree+=(_,_)=>{if(!prefs.ReduceMotion)timer.Start();};DetachedFromVisualTree+=(_,_)=>timer.Stop();Avalonia.Automation.AutomationProperties.SetName(this,"Available RAM");}
 public void Update(MemoryInfo value){memory=value;InvalidateVisual();}
 public override void Render(DrawingContext context){
  double percent=memory.FreeRatio*100;int heat=percent<prefs.CatRed?2:percent<prefs.CatOrange?1:0;
  var image=Assets.CatFrame((int)(phase*8)%8,heat);context.DrawImage(image,new Rect(0,0,image.Size.Width,image.Size.Height),new Rect(0,5,40,30));
 }
}
sealed class TokenGraph:Control {
 readonly Queue<long> values=new();Point? hover;
 public TokenGraph(){Height=78;PointerMoved+=(_,e)=>{hover=e.GetPosition(this);InvalidateVisual();};PointerExited+=(_,_)=>{hover=null;InvalidateVisual();};}
 public void Add(long value){values.Enqueue(value);while(values.Count>90)values.Dequeue();InvalidateVisual();}
 public override void Render(DrawingContext context){
  var points=values.ToArray();if(points.Length<2)return;double max=Math.Max(1,points.Max()),width=Bounds.Width,height=Bounds.Height-10;
  for(int i=1;i<points.Length;i++)context.DrawLine(new Pen(Ui.Mint,1.5),new Point((i-1)*width/(points.Length-1),height-points[i-1]/max*(height-8)),new Point(i*width/(points.Length-1),height-points[i]/max*(height-8)));
  if(hover is {} p){int index=Math.Clamp((int)Math.Round(p.X/Math.Max(1,width)*(points.Length-1)),0,points.Length-1);double x=index*width/(points.Length-1);context.DrawLine(new Pen(Ui.Muted,1),new Point(x,0),new Point(x,height));ToolTip.SetTip(this,$"{points[index]:N0} tokens");}
 }
}
