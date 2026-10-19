using System.Windows;
namespace TokenDock;

static class AdaptiveChecks {
 public static void Run(Action<bool> check,string dir){
  using var clipboard=new ClipboardModule();var surface=new NotchSurface{Running=false};surface.SetFixture();surface.Configure(new Preferences{Music=true,ClipboardHistory=true});
  foreach(string edge in new[]{"top","bottom","left","right"}){
   surface.SetEdge(edge);surface.Open=1;
   foreach(int count in new[]{0,1,2,3,20}){
    clipboard.History.Clear();for(int i=0;i<count;i++)clipboard.History.Add($"Copie {i+1}");surface.ShowModule("clipboard",new InlineModuleView(null,clipboard));
    double expected=34+66*Math.Clamp(count,1,3);check(surface.BodyHeight==expected);check(surface.ModuleBounds.Height==expected);
    var b=surface.ModuleBounds;check(new[]{b.TopLeft,b.TopRight,b.BottomLeft,b.BottomRight}.All(surface.Contains));
    if(count==1&&edge is "left" or "right"){check(!surface.Contains(new Point(b.X+100,120)));check(surface.Contains(new Point(edge=="right"?294:26,310)));}
    if(count<=3)surface.RenderFile(System.IO.Path.Combine(dir,$"clipboard-{count}-{edge}.png"),true,edge);
   }
   clipboard.Clear();check(surface.BodyHeight==100);surface.CloseModule();check(surface.BodyHeight==(edge is "left" or "right"?246:100));
   int created=0;FrameworkElement Create(string kind){created++;return new System.Windows.Controls.Border();}
   foreach(string module in new[]{"music","clipboard"}){
    var r=surface.ModuleRect(module);var p=new Point(r.X+r.Width/2,r.Y+r.Height/2);check(surface.TryOpenModule(p,Create));check(surface.ActiveModule==module);int before=created;check(surface.TryOpenModule(p,Create)&&created==before);
   }
   surface.CloseModule();
  }
  foreach(var side in new[]{"top","bottom","left","right"}){
   surface.Running=false;surface.Configure(new Preferences{Camera=true,ClipboardHistory=true,LayoutOrder=["clipboard","cat","camera"]});surface.SetEdge(side);surface.Open=1;surface.ShowModule("camera",new System.Windows.Controls.Border());var before=surface.ModuleBounds;surface.Running=true;clipboard.History.Clear();clipboard.History.Add("Photo fixture");surface.ShowModule("clipboard",new InlineModuleView(null,clipboard));
   check(!SystemParameters.ClientAreaAnimation||Math.Abs(surface.BodyHeight-before.Height)<1);check(!SystemParameters.ClientAreaAnimation||Math.Abs(surface.ModuleBounds.Top-before.Top)<1);surface.Running=false;surface.CloseModule();
  }
  foreach(var side in new[]{"top","bottom","left","right"})foreach(var from in new[]{"music","clipboard","camera","ram"})foreach(var to in new[]{"music","clipboard","camera","ram"}){
   if(from==to)continue;surface.Running=false;surface.CloseModule();surface.SetEdge(side);surface.Open=1;
   FrameworkElement Fixture(string kind)=>kind=="clipboard"?new InlineModuleView(null,clipboard):new System.Windows.Controls.Border();
   if(from!="ram")surface.ShowModule(from,Fixture(from));var before=surface.ModuleBounds;surface.Running=true;
   if(to=="ram")surface.CloseModule(true);else surface.ShowModule(to,Fixture(to));
   check(!SystemParameters.ClientAreaAnimation||Math.Abs(surface.BodyHeight-before.Height)<1);check(!SystemParameters.ClientAreaAnimation||Math.Abs(surface.ModuleBounds.Top-before.Top)<1);surface.Running=false;surface.CloseModule();
  }
  surface.Running=false;surface.SetEdge("right");surface.Open=1;surface.ShowModule("camera",new System.Windows.Controls.Border());surface.Running=true;
  foreach(var kind in new[]{"clipboard","music","camera","ram","clipboard","camera","music","ram"}){var before=surface.ModuleBounds;if(kind=="ram")surface.CloseModule(true);else surface.ShowModule(kind,kind=="clipboard"?new InlineModuleView(null,clipboard):new System.Windows.Controls.Border());check(!SystemParameters.ClientAreaAnimation||Math.Abs(surface.ModuleBounds.Top-before.Top)<1&&Math.Abs(surface.BodyHeight-before.Height)<1);}surface.Running=false;surface.CloseModule();
  surface.Configure(new Preferences());check(!surface.TryOpenModule(new Point(0,0),_=>throw new Exception("Disabled module")));
  surface.Configure(new Preferences{ClipboardHistory=true});surface.SetEdge("right");for(int i=0;i<3;i++)clipboard.History.Add($"Animation {i}");surface.ShowModule("clipboard",new InlineModuleView(null,clipboard));surface.Running=true;clipboard.Clear();check(!SystemParameters.ClientAreaAnimation||surface.HasAnimatedProperties);surface.Running=false;surface.CloseModule();
 }
}
