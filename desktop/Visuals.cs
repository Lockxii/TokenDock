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
}
static class Assets {
 static readonly Dictionary<string,Bitmap> cache=[];
 public static Bitmap Get(string name){if(!cache.TryGetValue(name,out var image))cache[name]=image=new Bitmap(Path.Combine(AppContext.BaseDirectory,"assets",name));return image;}
}
sealed class Cat:Control {
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(66)};double phase;MemoryInfo memory;Preferences prefs;
 public Cat(Preferences prefs){this.prefs=prefs;Width=40;Height=40;timer.Tick+=(_,_)=>{phase+=memory.CyclesPerSecond*prefs.CatSpeed*.066;InvalidateVisual();};AttachedToVisualTree+=(_,_)=>timer.Start();DetachedFromVisualTree+=(_,_)=>timer.Stop();Avalonia.Automation.AutomationProperties.SetName(this,"Available RAM");}
 public void Update(MemoryInfo value){memory=value;InvalidateVisual();}
 public override void Render(DrawingContext context){
  var image=Assets.Get("cat-run.png");int frame=(int)(phase*8)%8;double sw=image.Size.Width/8;
  context.DrawImage(image,new Rect(frame*sw,270,sw,200),new Rect(0,0,40,40));
  double percent=memory.FreeRatio*100;var color=percent<prefs.CatRed?Colors.IndianRed:percent<prefs.CatOrange?Colors.Orange:Colors.Transparent;
  context.DrawEllipse(new SolidColorBrush(color),null,new Point(35,5),3,3);
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
