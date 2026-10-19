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
 public static PixelPoint Locate(Edge edge,PixelRect area,PixelSize size,PixelPoint floating)=>edge switch {
  Edge.Top=>new(area.X+(area.Width-size.Width)/2,area.Y),
  Edge.Bottom=>new(area.X+(area.Width-size.Width)/2,area.Bottom-size.Height),
  Edge.Left=>new(area.X,area.Y+(area.Height-size.Height)/2),
  Edge.Right=>new(area.Right-size.Width,area.Y+(area.Height-size.Height)/2),
  _=>new(Math.Clamp(floating.X,area.X,Math.Max(area.X,area.Right-size.Width)),Math.Clamp(floating.Y,area.Y,Math.Max(area.Y,area.Bottom-size.Height)))
 };
}
