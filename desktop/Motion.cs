using Avalonia;
namespace TokenDock;
enum Edge { Top, Bottom, Left, Right, Floating }
sealed class Spring {
 public double Value,Velocity,Target;
 public Spring(double value){Value=Target=value;}
 public bool Step(double seconds,bool reduced=false){
  if(reduced){Value=Target;Velocity=0;return false;}
  double t=Math.Clamp(seconds,0,.05),offset=Value-Target,b=Velocity+26*offset,decay=Math.Exp(-26*t);
  Value=Target+(offset+b*t)*decay;Velocity=(Velocity-26*b*t)*decay;
  if(Math.Abs(Value-Target)<.15&&Math.Abs(Velocity)<.2){Value=Target;Velocity=0;return false;}return true;
 }
}
static class DockPlacement {
 public static PixelRect SelectArea(IEnumerable<PixelRect> areas,PixelPoint anchor){
  var valid=areas.Where(a=>a.Width>0&&a.Height>0).ToArray();
  if(valid.Length==0)return new PixelRect(0,0,1920,1080);
  double Distance(PixelRect a){double x=Math.Max(a.X-anchor.X,Math.Max(0,(double)anchor.X-a.Right+1)),y=Math.Max(a.Y-anchor.Y,Math.Max(0,(double)anchor.Y-a.Bottom+1));return x*x+y*y;}
  return valid.OrderBy(Distance).First();
 }
 public static PixelPoint Center(PixelRect a)=>new(a.X+a.Width/2,a.Y+a.Height/2);
 public static Edge Snap(PixelPoint p,PixelSize size,PixelRect area,double scale){
  double gap=20*Math.Clamp(scale,.5,4);
  if(Math.Abs((long)p.X-area.X)<gap)return Edge.Left;
  if(Math.Abs((long)p.X+size.Width-area.Right)<gap)return Edge.Right;
  if(Math.Abs((long)p.Y-area.Y)<gap)return Edge.Top;
  if(Math.Abs((long)p.Y+size.Height-area.Bottom)<gap)return Edge.Bottom;
  return Edge.Floating;
 }
 public static PixelPoint Locate(Edge edge,PixelRect area,PixelSize size,PixelPoint floating){
  size=new PixelSize(Math.Min(size.Width,area.Width),Math.Min(size.Height,area.Height));
  return edge switch {
  Edge.Top=>new(area.X+(area.Width-size.Width)/2,area.Y),
  Edge.Bottom=>new(area.X+(area.Width-size.Width)/2,area.Bottom-size.Height),
  Edge.Left=>new(area.X,area.Y+(area.Height-size.Height)/2),
  Edge.Right=>new(area.Right-size.Width,area.Y+(area.Height-size.Height)/2),
  _=>new(Math.Clamp(floating.X,area.X,Math.Max(area.X,area.Right-size.Width)),Math.Clamp(floating.Y,area.Y,Math.Max(area.Y,area.Bottom-size.Height)))
 };
 }
}
