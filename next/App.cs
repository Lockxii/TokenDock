using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using Forms=System.Windows.Forms;
namespace TokenDock;
static class Entry {
 [STAThread] public static void Main(string[] args){
  var app=new Application();
  if(args.Length==2&&args[0]=="--demo"){MediaDemo.Run(args[1]);app.Shutdown();return;}
  if(args.Contains("--check")){Checks.Run();app.Shutdown();return;}
  using var mutex=new Mutex(true,"Local\\TokenDock.Next",out bool first);if(!first)return;
  app.Run(new NotchWindow());
 }
}
sealed class NotchWindow:Window {
 readonly NotchSurface surface=new(); readonly Monitor monitor=new(); readonly DispatcherTimer poll=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly DispatcherTimer reveal=new(){Interval=TimeSpan.FromMilliseconds(60)},close=new(){Interval=TimeSpan.FromMilliseconds(200)};
 Preferences preferences=Preferences.Load();readonly CustomFeeds feeds=new();readonly ClipboardModule clipboard=new();readonly MusicModule music=new(Dispatcher.CurrentDispatcher);readonly Dictionary<string,Window> panels=[];int liveRefreshQueued;
 readonly MemoryConsumers consumers=new(); DateTime nextConsumers=DateTime.MinValue; bool samplingConsumers,closed;
 readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TokenDock","next-position.json");
 readonly System.Drawing.Icon trayIcon; readonly Forms.NotifyIcon tray; Forms.Screen screen=Forms.Screen.PrimaryScreen!;string edge="top";double anchor=.5,freeX=.5,freeY=.5;string? hoverCandidate;bool docked=true,dragging,moved,pinned;Point start,startOrigin;
 public NotchWindow(){
  var iconPath=Path.Combine(AppContext.BaseDirectory,"assets","tokendock.ico");trayIcon=new System.Drawing.Icon(iconPath,Forms.SystemInformation.SmallIconSize);Icon=BitmapFrame.Create(new Uri(iconPath));
  Title="TokenDock";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=null;ShowInTaskbar=false;Topmost=true;ShowActivated=false;Content=surface;
  feeds.Changed+=QueueLiveRefresh;monitor.Changed+=QueueLiveRefresh;music.Changed+=UpdateMusicPreview;feeds.Configure(preferences.Sources);surface.Configure(preferences);LoadSettings();surface.SetEdge(edge);surface.SetFloating(!docked,false);Width=surface.Width;Height=surface.Height;
  SourceInitialized+=(_,_)=>{Place();clipboard.Enable(new WindowInteropHelper(this).Handle,preferences.ClipboardHistory);_=music.Enable(preferences.Music);};DpiChanged+=(_,_)=>{if(!dragging)Dispatcher.BeginInvoke(Place);};
  AllowDrop=true;surface.AllowDrop=true;
  void AcceptFileDrag(object sender,DragEventArgs e){if(!preferences.ClipboardHistory||!e.Data.GetDataPresent(DataFormats.FileDrop,true))return;reveal.Stop();close.Stop();if(surface.ActiveModule!="clipboard")surface.ShowModule("clipboard",CreateModule("clipboard"));surface.Expand(true);e.Effects=(e.AllowedEffects&DragDropEffects.Copy);e.Handled=true;}
  PreviewDragEnter+=AcceptFileDrag;PreviewDragOver+=AcceptFileDrag;
  PreviewDrop+=async(_,e)=>{if(!preferences.ClipboardHistory||!e.Data.GetDataPresent(DataFormats.FileDrop,true))return;e.Handled=true;e.Effects=DragDropEffects.Copy;try{if(e.Data.GetData(DataFormats.FileDrop,true) is string[] files){var error=await clipboard.StoreFiles(files);if(error!=null)tray?.ShowBalloonTip(4000,"Unable to add files",error,Forms.ToolTipIcon.Warning);}}catch(Exception error)when(error is ExternalException or IOException or ArgumentException){tray?.ShowBalloonTip(4000,"Unable to add files",error.Message,Forms.ToolTipIcon.Warning);}};
  DragLeave+=(_,_)=>{if(!pinned)close.Start();};
  surface.MouseEnter+=(_,_)=>{close.Stop();if(!dragging&&!surface.IsIconDrag)reveal.Start();};surface.MouseLeave+=(_,_)=>{hoverCandidate=null;reveal.Stop();if(!dragging&&!surface.IsIconDrag&&!pinned&&surface.ContextMenu?.IsOpen!=true)close.Start();};
  reveal.Tick+=(_,_)=>{reveal.Stop();if(!dragging&&!surface.IsIconDrag){surface.SelectHover(Mouse.GetPosition(surface));if(!surface.TryOpenModule(Mouse.GetPosition(surface),CreateModule))surface.TryOpenMusicPreview(Mouse.GetPosition(surface),()=>CreateModule("music"));surface.Expand(true);}};close.Tick+=(_,_)=>{close.Stop();surface.Expand(false);};
  surface.MouseMove+=(_,e)=>{if(surface.IsIconDrag){surface.UpdateIconDrag(e.GetPosition(surface));e.Handled=true;return;}var point=e.GetPosition(surface);var candidate=surface.HoverKey(point);if(!dragging&&(candidate!=hoverCandidate||candidate is "music" or "clipboard" or "camera"&&surface.ActiveModule!=candidate&&!reveal.IsEnabled)){hoverCandidate=candidate;reveal.Stop();if(candidate!=null)reveal.Start();}if(candidate==null&&surface.ModuleBounds.Contains(point))surface.SelectHover(point);if(dragging){var p=PointToScreen(e.GetPosition(this));if((p-start).Length>5){if(!moved){moved=true;reveal.Stop();close.Stop();surface.SetFloating(true);surface.Expand(false);}Native.Move(new WindowInteropHelper(this).Handle,(int)(startOrigin.X+p.X-start.X),(int)(startOrigin.Y+p.Y-start.Y));}}};
  surface.MouseLeftButtonDown+=(_,e)=>{if(surface.IsModuleContent(e.GetPosition(surface)))return;if(surface.BeginIconDrag(e.GetPosition(surface))){reveal.Stop();close.Stop();surface.Focus();if(!surface.CaptureMouse())surface.CancelIconDrag();e.Handled=true;return;}surface.Focus();start=PointToScreen(e.GetPosition(this));startOrigin=PointToScreen(new Point(0,0));dragging=true;moved=false;surface.CaptureMouse();};
  surface.MouseLeftButtonUp+=(_,e)=>{if(surface.IsIconDrag){var drop=surface.EndIconDrag(e.GetPosition(surface));surface.ReleaseMouseCapture();if(drop!=null){if(drop.Moved){if(drop.Target!=null)SaveIconOrder(drop);}else if(drop.Source is "music" or "clipboard" or "camera")ShowTool(drop.Source);else{pinned=!pinned;surface.Expand(pinned);}}if(!pinned&&!surface.IsMouseOver)close.Start();e.Handled=true;return;}if(!dragging)return;dragging=false;surface.ReleaseMouseCapture();if(moved){pinned=false;FinishMove(PointToScreen(e.GetPosition(this)));}else{pinned=!pinned;surface.Expand(pinned);}e.Handled=true;};
  surface.LostMouseCapture+=(_,_)=>{if(surface.IsIconDrag){surface.CancelIconDrag();if(!pinned&&!surface.IsMouseOver)close.Start();return;}if(!dragging)return;dragging=false;if(moved){pinned=false;FinishMove(PointToScreen(Mouse.GetPosition(this)));}};
  KeyDown+=(_,e)=>{if(e.Key==Key.Escape){if(surface.IsIconDrag){surface.CancelIconDrag();surface.ReleaseMouseCapture();e.Handled=true;return;}surface.CloseModule();pinned=false;surface.Expand(false);}if(e.Key==Key.F1){pinned=!pinned;surface.Expand(pinned);}};
  var menu=BuildMenu();surface.ContextMenu=menu;
  tray=new Forms.NotifyIcon{Icon=trayIcon,Text="TokenDock · Double-click to show",Visible=true};var tm=new Forms.ContextMenuStrip();tm.Items.Add("Show",null,(_,_)=>Dispatcher.Invoke(()=>{Show();Place();}));tm.Items.Add("Customize…",null,(_,_)=>Dispatcher.Invoke(()=>ShowTool("settings")));tm.Items.Add("Quit",null,(_,_)=>Dispatcher.Invoke(Close));tray.ContextMenuStrip=tm;tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(()=>{Show();Place();});
  monitor.Start();poll.Tick+=(_,_)=>Update();poll.Start();Update();
  IsVisibleChanged+=(_,_)=>surface.Running=IsVisible;Closed+=(_,_)=>{closed=true;Save();poll.Stop();reveal.Stop();close.Stop();surface.Running=false;surface.CloseModule();monitor.Changed-=QueueLiveRefresh;feeds.Changed-=QueueLiveRefresh;music.Changed-=UpdateMusicPreview;monitor.Dispose();feeds.Dispose();clipboard.Dispose();music.Dispose();foreach(var panel in panels.Values.ToArray())panel.Close();tray.Dispose();trayIcon.Dispose();};
 }
 ContextMenu BuildMenu(){
  var menu=DockMenu.Create(()=>docked?edge:"floating",id=>{docked=true;edge=id;anchor=.5;surface.SetFloating(false);surface.SetEdge(edge);Width=surface.Width;Height=surface.Height;Place();Save();},()=>{monitor.Reset();feeds.Reset();Update();},Hide,Close,()=>ShowTool("settings"),preferences.Music?()=>ShowTool("music"):null,preferences.ClipboardHistory?()=>ShowTool("clipboard"):null);
  menu.Opened+=(_,_)=>{close.Stop();reveal.Stop();};
  menu.Closed+=(_,_)=>{if(!pinned&&!surface.IsMouseOver)close.Start();};return menu;
 }
 void SaveIconOrder(IconDrop drop){var next=preferences.Copy();next.MoveItem(drop.Source,drop.Target,drop.After);if(next.Layout.SequenceEqual(preferences.Layout))return;try{next.Save();ApplyPreferences(next);}catch(Exception error)when(error is IOException or UnauthorizedAccessException){tray.ShowBalloonTip(3500,"TokenDock","Could not save the order. Check access to settings and try again.",Forms.ToolTipIcon.Warning);}}
 Meter[] Snapshot()=>monitor.Snapshot().Concat(feeds.Snapshot()).ToArray();
 void UpdateMusicPreview(){if(!closed)surface.SetNowPlaying(music);}
 void QueueLiveRefresh(){
  if(closed||Interlocked.Exchange(ref liveRefreshQueued,1)!=0)return;
  if(Dispatcher.HasShutdownStarted){Interlocked.Exchange(ref liveRefreshQueued,0);return;}
  try{Dispatcher.BeginInvoke(()=>{Interlocked.Exchange(ref liveRefreshQueued,0);if(!closed)surface.Update(Snapshot(),MemoryInfo.Read(),false);},DispatcherPriority.Background);}catch(InvalidOperationException){Interlocked.Exchange(ref liveRefreshQueued,0);}
 }
 void ApplyPreferences(Preferences value){
  preferences=value.Copy();feeds.Configure(preferences.Sources);surface.Configure(preferences);surface.ContextMenu=BuildMenu();
  clipboard.Enable(new WindowInteropHelper(this).Handle,preferences.ClipboardHistory);_=music.Enable(preferences.Music);
  if(!preferences.Camera&&surface.ActiveModule=="camera"||!preferences.Music&&surface.ActiveModule=="music"||!preferences.IsShown("clipboard")&&surface.ActiveModule=="clipboard")surface.CloseModule();Update();
 }
 FrameworkElement CreateModule(string kind)=>kind=="camera"?new CameraPane((path,image)=>{clipboard.AddPhoto(path,image);ShowTool("clipboard");}):new InlineModuleView(kind=="music"?music:null,kind=="clipboard"?clipboard:null);
 void ShowTool(string kind){
  if(kind is "music" or "clipboard" or "camera"){
   if(kind=="camera"&&!preferences.Camera||kind=="music"&&!preferences.Music||kind=="clipboard"&&!preferences.IsShown("clipboard"))return;
   reveal.Stop();close.Stop();pinned=true;Show();surface.Focus();
   if(surface.ActiveModule!=kind)surface.ShowModule(kind,CreateModule(kind));
   surface.Expand(true);return;
  }
  if(panels.TryGetValue(kind,out var current)){current.Activate();return;}
  ToolPanelWindow? panel=null;UIElement view=new PersonalizationView(preferences,ApplyPreferences,()=>panel?.Close(),Snapshot);
  panel=new ToolPanelWindow("Customize",view){Owner=this};panels[kind]=panel;
  panel.Closed+=(_,_)=>{if(view is IDisposable disposable)disposable.Dispose();panels.Remove(kind);};panel.Show();
 }
 async void Update(){
  surface.Update(Snapshot(),MemoryInfo.Read());
  if(closed||samplingConsumers||DateTime.UtcNow<nextConsumers)return;
  samplingConsumers=true;nextConsumers=DateTime.UtcNow.AddSeconds(10);
  try{var values=await Task.Run(consumers.Read);if(!closed)surface.SetConsumers(values);}
  catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException){if(!closed)surface.SetConsumers([]);}
  finally{samplingConsumers=false;}
 }
 double Dpi()=>VisualTreeHelper.GetDpi(this).DpiScaleX;
 void Place(){
  var handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;
  screen=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==screen.DeviceName)??Forms.Screen.PrimaryScreen!;
  var a=screen.Bounds;double dpi=Native.DpiAt(a.Left+a.Width/2,a.Top+a.Height/2);
  var pos=docked?LayoutMath.Bounds(edge,a.Left,a.Top,a.Width,a.Height,dpi,anchor):LayoutMath.FloatingBounds(edge,a,dpi,freeX,freeY);Native.Place(handle,pos);
 }
 void FinishMove(Point p){
  screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)p.X,(int)p.Y));var area=screen.Bounds;
  double dpi=Native.DpiAt(area.Left+area.Width/2,area.Top+area.Height/2);var snap=LayoutMath.NearEdge(p,area,dpi);
  if(snap!=null){docked=true;edge=snap;anchor=edge is "left" or "right"?(p.Y-area.Top)/area.Height:(p.X-area.Left)/area.Width;}
  else {
   if(!Native.TryWindowBounds(new WindowInteropHelper(this).Handle,out var current))return;docked=false;var size=LayoutMath.FloatingBounds(edge,area,dpi,0,0);
   (freeX,freeY)=LayoutMath.Fractions(current,area,size.Width,size.Height);
  }
  surface.SetEdge(edge);surface.SetFloating(!docked);Width=surface.Width;Height=surface.Height;Place();Save();
 }
 void LoadSettings(){try{if(!File.Exists(settings))return;var p=JsonSerializer.Deserialize<Saved>(File.ReadAllText(settings));if(p==null)return;if(new[]{"top","bottom","left","right"}.Contains(p.Edge))edge=p.Edge;anchor=LayoutMath.Unit(p.Anchor);docked=p.Docked;freeX=LayoutMath.Unit(p.FreeX);freeY=LayoutMath.Unit(p.FreeY);screen=Forms.Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==p.Screen)??screen;}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){}}
 void Save(){try{Directory.CreateDirectory(Path.GetDirectoryName(settings)!);File.WriteAllText(settings,JsonSerializer.Serialize(new Saved(edge,anchor,screen.DeviceName,docked,freeX,freeY)));}catch(IOException){}catch(UnauthorizedAccessException){}}
 internal record Saved(string Edge,double Anchor,string Screen,bool Docked=true,double FreeX=.5,double FreeY=.5);
}
static class LayoutMath {
 public static double Unit(double value)=>double.IsFinite(value)?Math.Clamp(value,0,1):.5;
 // Snap only when the release pointer is within 24 logical pixels of a monitor edge.
 public static string? NearEdge(Point p,System.Drawing.Rectangle area,double dpi){
  double[] distances=[Math.Abs(p.Y-area.Top),Math.Abs(p.Y-area.Bottom),Math.Abs(p.X-area.Left),Math.Abs(p.X-area.Right)];int nearest=Array.IndexOf(distances,distances.Min());
  return distances[nearest]<=24*dpi?new[]{"top","bottom","left","right"}[nearest]:null;
 }
 public static System.Drawing.Rectangle FloatingBounds(string edge,System.Drawing.Rectangle area,double dpi,double x,double y){
  var size=Bounds(edge,0,0,area.Width,area.Height,dpi,.5);
  return new(area.X+(int)Math.Round(Unit(x)*Math.Max(0,area.Width-size.Width)),area.Y+(int)Math.Round(Unit(y)*Math.Max(0,area.Height-size.Height)),size.Width,size.Height);
 }
 public static (double X,double Y) Fractions(System.Drawing.Rectangle window,System.Drawing.Rectangle area,int width,int height)=>(Unit((window.X-area.X)/(double)Math.Max(1,area.Width-width)),Unit((window.Y-area.Y)/(double)Math.Max(1,area.Height-height)));

 public static System.Drawing.Rectangle Bounds(string edge,int x,int y,int width,int height,double dpi,double anchor){bool vertical=edge is "left" or "right";int w=(int)Math.Round((vertical?320:660)*dpi),h=(int)Math.Round((vertical?364:308)*dpi);int along=(int)Math.Round(anchor*(vertical?height:width)-(vertical?h:w)/2d);
  return vertical?new(edge=="left"?x:x+width-w,y+Math.Clamp(along,0,Math.Max(0,height-h)),w,h):new(x+Math.Clamp(along,0,Math.Max(0,width-w)),edge=="top"?y:y+height-h,w,h);
 }
}
static class Native {
 [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll",SetLastError=true)]static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
 public static bool TryWindowBounds(IntPtr hwnd,out System.Drawing.Rectangle bounds){bool ok=GetWindowRect(hwnd,out var r);bounds=ok?System.Drawing.Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom):System.Drawing.Rectangle.Empty;return ok;}

 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromPoint(System.Drawing.Point point,uint flags);
 [DllImport("shcore.dll")]static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
 public static double DpiAt(int x,int y){return GetDpiForMonitor(MonitorFromPoint(new(x,y),2),0,out var dx,out _)==0?dx/96d:1;}
 public static void Place(IntPtr hwnd,System.Drawing.Rectangle r)=>SetWindowPos(hwnd,IntPtr.Zero,r.X,r.Y,r.Width,r.Height,0x14);
 public static void Move(IntPtr hwnd,int x,int y)=>SetWindowPos(hwnd,IntPtr.Zero,x,y,0,0,0x15);
}
static class Checks {
 public static void Run(){int n=0;void Check(bool value){if(!value)throw new Exception("Check failed "+n);n++;}
  foreach(var dpi in new[]{1d,1.25,1.5,2})foreach(var side in new[]{"top","bottom","left","right"}){var r=LayoutMath.Bounds(side,-2560,0,2560,1440,dpi,.5);Check(side switch{"top"=>r.Top==0,"bottom"=>r.Bottom==1440,"left"=>r.Left==-2560,_=>r.Right==0});Check(r.Left>=-2560&&r.Right<=0&&r.Top>=0&&r.Bottom<=1440);}
  Check(new MemoryInfo(100,10).CyclesPerSecond>new MemoryInfo(100,80).CyclesPerSecond);Check(NotchSurface.Shape(560,48).FillContains(new Point(280,24)));Check(!NotchSurface.Shape(560,48).FillContains(new Point(0,47)));
  using(var d=JsonDocument.Parse("{\"input_tokens\":100,\"cached_input_tokens\":80,\"output_tokens\":20}"))Check(Parse.Codex(d.RootElement).Total==120);
  var s=new NotchSurface{Running=false};
  s.SetEdge("top");s.Open=0;Check(!s.Contains(new Point(330,80)));Check(s.Contains(new Point(330,24)));
  s.SelectHover(new Point(85,24));Check(s.Selected==1);s.Open=1;s.SelectHover(new Point(161,24));Check(s.Selected==0);s.SelectHover(new Point(500,100));Check(s.Selected==-1);
s.Update([new Meter("Codex"),new Meter("Claude Code"),new Meter("OpenCode")],MemoryInfo.Read());var dir=AppContext.BaseDirectory;s.RenderFile(Path.Combine(dir,"compact.png"),false,"top");s.RenderFile(Path.Combine(dir,"expanded.png"),true,"top");s.RenderFile(Path.Combine(dir,"vertical.png"),true,"right");s.SetFixture();s.RenderFile(Path.Combine(dir,"design-rest.png"),false,"top");s.RenderFile(Path.Combine(dir,"design-hover.png"),true,"top");s.RenderFile(Path.Combine(dir,"design-transition.png"),true,"top",.5);s.RenderFile(Path.Combine(dir,"design-bottom.png"),true,"bottom");s.RenderFile(Path.Combine(dir,"design-side.png"),true,"left");
  s.SetFixture();Check(s.OverallTotal==58700);Check(s.SamplesFor(-1).Last()==27);
  var graph=new Rect(316,70,245,43);Check(NotchSurface.SampleIndex(graph,new Point(316,80),14)==0);Check(NotchSurface.SampleIndex(graph,new Point(561,80),14)==13);Check(NotchSurface.SampleIndex(graph,new Point(315,80),14)==-1);Check(NotchSurface.SampleIndex(graph,new Point(400,80),0)==-1);
  s.SetEdge("top");s.Open=1;s.SelectHover(new Point(124,26));Check(s.Selected==1);s.SelectHover(new Point(450,90));Check(s.Selected==-1);s.RenderFile(Path.Combine(dir,"design-graph-hover.png"),true,"top");
  Check(s.Height==308);Check(s.Contains(new Point(114,159)));Check(s.Contains(new Point(561,159)));
  s.SetEdge("bottom");s.Open=1;s.SelectHover(new Point(173,s.Height-176+26));Check(s.Selected==0);s.ClearHover();Check(s.Selected==-1);
  var apps=new MemoryConsumers().Read();Check(apps.Length<=3);Check(apps.All(a=>a.Bytes>=0&&a.Count>0&&(a.Icon==null||a.Icon.IsFrozen)));Check(apps.SequenceEqual(apps.OrderByDescending(a=>a.Bytes)));
  s.Update([new Meter("Codex"),new Meter("Claude Code"),new Meter("OpenCode")],MemoryInfo.Read());s.SetConsumers(apps);s.SetEdge("top");s.Open=1;s.SelectHover(new Point(540,26));s.RenderFile(Path.Combine(dir,"design-memory.png"),true,"top");s.SelectHover(new Point(361,147));s.RenderFile(Path.Combine(dir,"design-memory-hover.png"),true,"top");
  // Side panels use only the fixed rail for provider selection.
  foreach(var side in new[]{"left","right"}){
   s.SetFixture();s.SetEdge(side);s.Open=1;
   var first=s.VerticalRailIconBounds(0);s.SelectHover(new Point(first.X+11,first.Y+11));Check(s.Selected==1);s.RenderFile(Path.Combine(dir,$"design-{side}-selected.png"),true,side);
   double panelX=side=="right"?28:80;for(int i=0;i<3;i++){s.SelectHover(new Point(panelX+24+i*49,83));Check(s.Selected==-1);}
   var r=s.VerticalGraphBounds;foreach(var point in new[]{r.TopLeft,r.TopRight,r.BottomLeft,r.BottomRight,new Point(r.X,r.Top-28)})Check(s.Contains(point));
   s.SelectHover(new Point(r.X+r.Width/2,r.Y+r.Height/2));Check(s.Selected==-1);s.RenderFile(Path.Combine(dir,$"design-{side}-graph.png"),true,side);
   foreach(double progress in new[]{.25,.5,.75,1}){s.Open=progress;var transform=s.VerticalContentMatrix;Check(s.Contains(transform.Transform(new Point(r.Left,64))));Check(s.Contains(transform.Transform(new Point(r.Right,132))));Check(s.Contains(transform.Transform(new Point(r.Left,319))));Check(s.Contains(transform.Transform(new Point(r.Right,322))));}
   s.ClearHover();s.RenderFile(Path.Combine(dir,$"design-{side}-transition.png"),true,side,.75);s.RenderFile(Path.Combine(dir,$"design-{side}-rest.png"),false,side);
  }
  foreach(var side in new[]{"left","right"}){
   s.SetFixture();s.SetEdge(side);
   foreach(double progress in new[]{0,.25,.5,.75,1}){
    s.Open=progress;
    foreach(var rect in s.VerticalRailContentBounds())Check(new[]{rect.TopLeft,rect.TopRight,rect.BottomLeft,rect.BottomRight}.All(s.Contains));
    for(int i=0;i<3;i++){var icon=s.VerticalRailIconBounds(i);s.SelectHover(new Point(icon.X+11,icon.Y+11));Check(s.Selected==new[]{1,0,2}[i]);}
   }
  }
  s.SetFixture();s.ClearHover();s.RenderFile(Path.Combine(dir,"design-left-rest.png"),false,"left");s.RenderFile(Path.Combine(dir,"design-right-rest.png"),false,"right");
  Check(s.Height==364);
  string activeEdge="right";int resets=0,hides=0,quits=0;
  var menu=DockMenu.Create(()=>activeEdge,id=>activeEdge=id,()=>resets++,()=>hides++,()=>quits++);
  Check(menu.Items.Count==8);Check(((MenuItem)menu.Items[3]).IsChecked);
  ((MenuItem)menu.Items[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check(activeEdge=="left");Check(menu.Items.OfType<MenuItem>().Count(m=>m.IsChecked)==1);
  for(int i=5;i<8;i++)((MenuItem)menu.Items[i]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check(resets==1&&hides==1&&quits==1);
  menu.ApplyTemplate();menu.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));menu.Arrange(new Rect(menu.DesiredSize));menu.UpdateLayout();Check(menu.ActualWidth==250);Check(menu.Template!=null);Check(!menu.IsOpen);
  var menuBitmap=new RenderTargetBitmap((int)menu.ActualWidth*2,(int)menu.ActualHeight*2,192,192,PixelFormats.Pbgra32);menuBitmap.Render(menu);var menuEncoder=new PngBitmapEncoder();menuEncoder.Frames.Add(BitmapFrame.Create(menuBitmap));using(var file=File.Create(Path.Combine(dir,"design-menu.png")))menuEncoder.Save(file);
  var motion=new SpringAnimation{From=0,To=1,Frequency=15,Duration=TimeSpan.FromSeconds(.8)};
  Check(motion.Sample(0)==(0d,0d));Check(motion.Sample(.8)==(1d,0d));Check(motion.Sample(.79).Position>.999);
  double last=0;for(int i=0;i<=80;i++){var value=motion.Sample(i/100d);Check(value.Position>=last&&value.Position<=1);last=value.Position;}
  var interrupted=motion.Sample(.12);var reverse=new SpringAnimation{From=interrupted.Position,To=0,Velocity=interrupted.Velocity,Frequency=15,Duration=TimeSpan.FromSeconds(.8)};
  Check(reverse.Sample(0)==interrupted);Check(Math.Abs((reverse.Sample(.00001).Position-reverse.From)/.00001-interrupted.Velocity)<.01);Check(reverse.Sample(.8)==(0d,0d));
  var clone=(SpringAnimation)motion.Clone();Check(clone.Sample(.2)==motion.Sample(.2));
  var clock=(AnimationClock)motion.CreateClock(true);clock.Controller!.Begin();clock.Controller.SeekAlignedToLastTick(TimeSpan.FromSeconds(.2),TimeSeekOrigin.BeginTime);Check(Math.Abs((double)motion.GetCurrentValue(0d,1d,clock)-motion.Sample(.2).Position)<.00001);clock.Controller.Remove();
  foreach(var side in new[]{"top","bottom","left","right"}){
   s.SetFixture();s.SetEdge(side);
   foreach(double opening in new[]{0,.5,1})foreach(double detach in new[]{0,.5,1}){
    s.Open=opening;s.Floating=detach;
    if(side is "left" or "right")foreach(var rect in s.VerticalRailContentBounds())Check(new[]{rect.TopLeft,rect.TopRight,rect.BottomLeft,rect.BottomRight}.All(s.Contains));
   }
   s.Floating=1;s.Open=0;double length=side is "left" or "right"?364:660,depth=side is "left" or "right"?52:48;var shape=NotchSurface.Shape(length,depth,1);
   Check(!shape.FillContains(new Point(1,1)));Check(!shape.FillContains(new Point(length-1,1)));Check(!shape.FillContains(new Point(1,depth-1)));Check(!shape.FillContains(new Point(length-1,depth-1)));Check(shape.FillContains(new Point(length/2,depth/2)));
   s.RenderFile(Path.Combine(dir,$"design-floating-{side}.png"),false,side);
  }
  s.Floating=0;s.SetFixture();var frames=Path.Combine(Environment.CurrentDirectory,"work","motion-v9");Directory.CreateDirectory(frames);
  var closing=new SpringAnimation{From=1,To=0,Frequency=15,Duration=TimeSpan.FromSeconds(.8)};
  for(int frame=0;frame<65;frame++){double time=frame/30d;double value=time<=.8?motion.Sample(time).Position:time<=1.25?1:closing.Sample(time-1.25).Position;s.RenderFile(Path.Combine(frames,$"frame-{frame:000}.png"),true,"right",value);}
  s.Floating=0;s.SetFixture();s.RenderFile(Path.Combine(dir,"design-right-rest.png"),false,"right");
  foreach(var side in new[]{"left","right"}){s.SetFixture();s.SetConsumers(apps);s.SetEdge(side);s.Open=1;s.SelectHover(new Point(side=="right"?294:26,290));s.RenderFile(Path.Combine(dir,$"design-{side}-memory.png"),true,side);}
  foreach(var area in new[]{new System.Drawing.Rectangle(0,0,1920,1080),new System.Drawing.Rectangle(-2560,-200,2560,1440)})foreach(var dpi in new[]{1d,1.25,1.5,2}){
   double cx=area.Left+area.Width/2d,cy=area.Top+area.Height/2d;Check(LayoutMath.NearEdge(new Point(cx,cy),area,dpi)==null);
   Check(LayoutMath.NearEdge(new Point(cx,area.Top+24*dpi),area,dpi)=="top");Check(LayoutMath.NearEdge(new Point(cx,area.Bottom-24*dpi),area,dpi)=="bottom");Check(LayoutMath.NearEdge(new Point(area.Left+24*dpi,cy),area,dpi)=="left");Check(LayoutMath.NearEdge(new Point(area.Right-24*dpi,cy),area,dpi)=="right");Check(LayoutMath.NearEdge(new Point(cx,area.Top+24*dpi+1),area,dpi)==null);
   foreach(var side in new[]{"top","bottom","left","right"}){
    var placed=LayoutMath.FloatingBounds(side,area,dpi,.37,.61);Check(area.Contains(placed));var fractions=LayoutMath.Fractions(placed,area,placed.Width,placed.Height);Check(LayoutMath.FloatingBounds(side,area,dpi,fractions.X,fractions.Y)==placed);
    var limited=LayoutMath.FloatingBounds(side,area,dpi,-1,2);Check(limited.Left==area.Left&&limited.Bottom==area.Bottom);
   }
  }
  var legacy=JsonSerializer.Deserialize<NotchWindow.Saved>("{\"Edge\":\"left\",\"Anchor\":0.4,\"Screen\":\"test\"}");Check(legacy!.Docked);Check(legacy.FreeX==.5&&legacy.FreeY==.5);
  var freeSaved=new NotchWindow.Saved("right",.5,"test",false,.37,.61);Check(JsonSerializer.Deserialize<NotchWindow.Saved>(JsonSerializer.Serialize(freeSaved))==freeSaved);Check(LayoutMath.Unit(double.NaN)==.5);
  s.SetFixture();s.SetFloating(true,false);s.RenderFile(Path.Combine(dir,"design-free-expanded.png"),true,"top");Check(s.Floating==1);s.SetFloating(false,false);Check(s.Floating==0);
  ModuleChecks.Run(Check,dir);
  InlineChecks.Run(Check,dir);
  MusicChecks.Run(Check,dir);
  CompactMusicChecks.Run(Check,dir);
  ClipboardChecks.Run(Check,dir);
  AdaptiveChecks.Run(Check,dir);
  EditorChecks.Run(Check,dir);
  DirectDragChecks.Run(Check,dir);
  ShelfChecks.Run(Check,dir);
  File.WriteAllText(Path.Combine(dir,"checks.txt"),$"PASS {n} checks. Offscreen renders only; no desktop window opened.");
 }
}

