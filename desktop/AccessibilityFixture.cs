using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
namespace TokenDock;

static class AccessibilityFixture {
 internal static void Start(DockWindow dock,string directory,IClassicDesktopStyleApplicationLifetime app){
  if(!File.Exists(Path.Combine(directory,"fixture.marker")))throw new InvalidOperationException("Missing synthetic fixture marker.");
  string current="";var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
  timer.Tick+=(_,_)=>{
   if(File.Exists(Path.Combine(directory,"stop"))){timer.Stop();app.Shutdown();return;}
   string path=Path.Combine(directory,"module");if(!File.Exists(path))return;
   string module=File.ReadAllText(path).Trim();if(module==current)return;
   if(module is not("all" or "music" or "clipboard" or "camera" or "cat" or "settings"))throw new InvalidOperationException("Unknown fixture module.");
   current=module;dock.ShowModule(module);dock.Settle();
   Dispatcher.UIThread.Post(()=>File.WriteAllText(Path.Combine(directory,"ready"),module),DispatcherPriority.Background);
  };
  dock.Opened+=(_,_)=>timer.Start();dock.Closed+=(_,_)=>timer.Stop();
 }
}
