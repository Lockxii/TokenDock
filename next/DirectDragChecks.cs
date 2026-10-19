using System.Windows;
namespace TokenDock;

static class DirectDragChecks {
 static Point Center(Rect r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
 public static void Run(Action<bool> check,string dir){
  foreach(bool custom in new[]{false,true})foreach(string edge in new[]{"top","bottom","left","right"})foreach(double open in new[]{0d,1d}){
   var p=new Preferences{Music=true,ClipboardHistory=true};if(custom)p.MoveItem("cat","claude");var surface=new NotchSurface{Running=false};surface.SetFixture();surface.Configure(p);surface.SetEdge(edge);surface.Open=open;
   foreach(string id in p.Layout){var center=Center(surface.VisibleItemBounds(id));check(surface.BeginIconDrag(center));check(surface.IsIconDrag);var click=surface.EndIconDrag(new Point(center.X+2,center.Y+1));check(click is{Moved:false}&&click.Source==id);check(!surface.IsIconDrag);}
   var origin=Center(surface.VisibleItemBounds("cat"));var target=surface.VisibleItemBounds("codex");bool vertical=edge is "left" or "right";var before=vertical?new Point(target.X+target.Width/2,target.Y+1):new Point(target.X+1,target.Y+target.Height/2);
   check(surface.BeginIconDrag(origin));surface.UpdateIconDrag(before);var drop=surface.EndIconDrag(before);check(drop is{Moved:true,Target:"codex",After:false});var next=p.Copy();next.MoveItem(drop!.Source,drop.Target,drop.After);check(Array.IndexOf(next.Layout,"cat")+1==Array.IndexOf(next.Layout,"codex"));
   check(surface.BeginIconDrag(origin));surface.UpdateIconDrag(new Point(-100,-100));check(surface.EndIconDrag(new Point(-100,-100)) is{Moved:true,Target:null});check(!surface.IsIconDrag);
   check(surface.BeginIconDrag(origin));surface.UpdateIconDrag(before);surface.CancelIconDrag();check(!surface.IsIconDrag&&surface.EndIconDrag(before)==null);check(Center(surface.VisibleItemBounds("cat"))==origin);
   check(!surface.BeginIconDrag(new Point(-100,-100)));
   if(custom&&open==1){surface.BeginIconDrag(origin);surface.UpdateIconDrag(before);surface.RenderFile(Path.Combine(dir,$"direct-drag-{edge}.png"),true,edge);surface.CancelIconDrag();}
  }
  var slots=new Dictionary<string,Rect>{{"music",new Rect(0,0,26,26)},{"cat",new Rect(0,50,26,26)}};var gesture=new IconGesture("music",new Point(13,13),true,slots);check(!gesture.Drop(new Point(16,15)).Moved);gesture.Update(new Point(13,74));check(gesture.Moved&&gesture.Target=="cat"&&gesture.After);gesture.Update(new Point(100,74));check(gesture.Target==null);
 }
}
