using System.Windows;
namespace TokenDock;
static class ShelfChecks {
 public static void Run(Action<bool> check,string dir){
  Task.Run(async()=>{
   string fixture=Path.Combine(dir,"shelf-fixture.txt");File.WriteAllText(fixture,"original");
   using var shelf=new FileShelf();var saved=await shelf.Store([fixture]);
   check(saved.Files[0]!=fixture&&File.ReadAllText(saved.Files[0])=="original");
   File.WriteAllText(fixture,"changed");check(File.ReadAllText(saved.Files[0])=="original");
   var duplicates=await shelf.Store([fixture,fixture]);check(duplicates.Files.Distinct().Count()==2);
   try{await shelf.Store([dir]);check(false);}catch(IOException){check(true);}
   try{await shelf.Store([fixture,Path.Combine(dir,"missing-fixture")]);check(false);}catch(IOException){check(true);}
   check(File.ReadAllText(fixture)=="changed");
  }).GetAwaiter().GetResult();
  check(NotchSurface.CatWarmth(.5)==0&&NotchSurface.CatWarmth(.2)==1);check(NotchSurface.CatHeat(.2)==0&&NotchSurface.CatHeat(.04)==1);
  var settings=new Preferences{CatOrange=35,CatRed=12,CatSpeed=1.5};var restored=settings.Copy();check(restored.CatOrange==35&&restored.CatRed==12&&restored.CatSpeed==1.5);settings.CatOrange=10;settings.CatRed=30;settings.CatSpeed=100;settings.Normalize();check(settings.CatRed==9&&settings.CatSpeed==2);settings.CatOrange=double.NaN;settings.CatRed=double.PositiveInfinity;settings.Normalize();check(settings.CatOrange==40&&settings.CatRed==20);
  var cameraSettings=new Preferences{Camera=true};cameraSettings.Normalize();check(cameraSettings.Layout.Contains("camera")&&cameraSettings.Copy().Camera);cameraSettings.MoveItem("camera","claude");check(cameraSettings.Layout[0]=="camera");cameraSettings.ShowItem("camera",false);check(!cameraSettings.Layout.Contains("camera"));
  var cameraSurface=new NotchSurface{Running=false};cameraSettings.ShowItem("camera",true);cameraSurface.Configure(cameraSettings);foreach(var side in new[]{"top","bottom","left","right"}){cameraSurface.SetEdge(side);var icon=cameraSurface.ItemBounds("camera");check(cameraSurface.ActionAt(new Point(icon.X+icon.Width/2,icon.Y+icon.Height/2))=="camera");check(cameraSurface.HoverKey(new Point(icon.X-2,icon.Y+icon.Height/2))=="camera");check(cameraSurface.TryOpenModule(new Point(icon.X-2,icon.Y+icon.Height/2),_=>new System.Windows.Controls.Border()));cameraSurface.ShowModule("camera",new CameraPane());cameraSurface.RenderFile(Path.Combine(dir,$"camera-idle-{side}.png"),true,side);cameraSurface.CloseModule();}
  var surface=new NotchSurface{Running=false};surface.Configure(new Preferences{LayoutOrder=["claude","codex","opencode","cat"]});surface.SetEdge("top");surface.Open=1;
  var cat=surface.ItemBounds("cat");surface.SelectHover(new Point(cat.X+15,cat.Y+15));
  surface.SelectHover(new Point(cat.X+15,45));surface.SelectHover(new Point(330,100));
  check(surface.IsRamDetail);
  surface.RenderFile(Path.Combine(dir,"shelf-ram-hover.png"),true,"top");
  foreach(int available in new[]{60,25,5}){surface.Update([],new MemoryInfo(100,(ulong)available));surface.RenderFile(Path.Combine(dir,$"cat-memory-{available}.png"),false,"top");}

 }
}
