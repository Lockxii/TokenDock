using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TokenDock;
sealed class DockWindow:Window,IDisposable {
 readonly bool fixture;readonly Canvas canvas=new();readonly Border rail=new(){Background=Brushes.Black},panel=new(){Background=Brushes.Black,Padding=new Thickness(24),ClipToBounds=true};
 readonly StackPanel icons=new(){Spacing=12};readonly ContentControl content=new();
 readonly Spring width=new(440),height=new(72),bodyHeight=new(240);readonly Stopwatch clock=Stopwatch.StartNew();double last;
 readonly DispatcherTimer motion=new(){Interval=TimeSpan.FromMilliseconds(16)},sample=new(){Interval=TimeSpan.FromSeconds(1)},hover=new(){Interval=TimeSpan.FromMilliseconds(45)},close=new(){Interval=TimeSpan.FromMilliseconds(180)};
 readonly Monitor meter=new();readonly CustomFeeds custom=new();readonly Music music=new();readonly Camera camera;
 readonly Copies copies;readonly List<Bitmap> transient=[];readonly Dictionary<string,Button> buttons=[];readonly Dictionary<string,TextBlock> counts=[];readonly Dictionary<string,TokenGraph> graphs=[];
 readonly DispatcherTimer live=new(){Interval=TimeSpan.FromMilliseconds(60)};int refreshPending;
 Preferences prefs;Cat? cat;Edge edge=Edge.Top;bool expanded,disposed,dragging,reordering,reduced;string selected="all",pending="all",pressedId="";
 Point press;PixelPoint dragOffset,floating,anchor;bool shelfDragging;TrayIcon? tray;MemoryInfo memory;Meter[] meters=[];
 TextBlock? featured,counter,details,ramNumber,ramPercent,musicTitle,musicArtist,musicTime,cameraStatus,errorLabel;Slider? seek;ProgressBar? ramBar;Image? cameraImage,musicImage;Button? play;Bitmap? frameBitmap,artBitmap,barArt;byte[]? lastArt,panelArt;
 readonly StackPanel copyCards=new(){Spacing=8};double targetBody=240;int tick;
 public DockWindow(bool fixture=false){
  this.fixture=fixture;prefs=fixture?new Preferences{Music=true,ClipboardHistory=true,Camera=true}:Preferences.Load();prefs.Normalize();Enum.TryParse(prefs.DockEdge,out edge);floating=anchor=new PixelPoint(prefs.DockX,prefs.DockY);reduced=prefs.ReduceMotion;camera=new Camera(fixture);copies=new Copies(this);if(fixture){byte[] pixels=new byte[128*128*4];for(int y=0;y<128;y++)for(int x=0;x<128;x++){int n=(y*128+x)*4;pixels[n]=180;pixels[n+1]=(byte)(y*2);pixels[n+2]=(byte)(x*2);pixels[n+3]=255;}var frame=new CameraFrame(128,128,pixels);camera.SetFixture(frame);music.SetFixture(frame.RoundPng());copies.Add(CopyItem.FromText("A synthetic clipboard entry")!);copies.Add(CopyItem.FromPng(frame.RoundPng())!);}
  Title="TokenDock";WindowDecorations=WindowDecorations.None;CanResize=false;ShowInTaskbar=false;Topmost=true;Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
  Width=440;Height=72;Content=canvas;panel.Child=new ScrollViewer{Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto};canvas.Children.Add(panel);canvas.Children.Add(rail);rail.Child=new ScrollViewer{Content=icons,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden};panel.IsVisible=false;
  panel.PointerEntered+=(_,_)=>close.Stop();rail.PointerEntered+=(_,_)=>{close.Stop();if(!expanded&&!dragging&&!reordering)ShowModule("all");};PointerExited+=(_,_)=>{if(!dragging&&!reordering&&!shelfDragging)close.Start();};close.Tick+=(_,_)=>{close.Stop();if(!shelfDragging&&!panel.IsKeyboardFocusWithin)Collapse();};
  hover.Tick+=(_,_)=>{hover.Stop();ShowModule(pending);};motion.Tick+=(_,_)=>Animate();
  sample.Tick+=(_,_)=>Sample();live.Tick+=(_,_)=>{live.Stop();Interlocked.Exchange(ref refreshPending,0);meters=meter.Snapshot();SampleContent();};meter.Changed+=ScheduleRead;custom.Changed+=ScheduleRead;
  rail.PointerPressed+=BeginMove;rail.PointerMoved+=Move;rail.PointerReleased+=EndMove;
  KeyDown+=(_,e)=>{if(e.Key==Key.Escape){var focus=selected;Collapse();if(buttons.TryGetValue(focus,out var origin))origin.Focus();e.Handled=true;}};
  DragDrop.SetAllowDrop(this,true);AddHandler(DragDrop.DragOverEvent,DragOver);AddHandler(DragDrop.DropEvent,Drop);AddHandler(DragDrop.DragLeaveEvent,(_,_)=>{if(!shelfDragging)close.Start();});
  Screens.Changed+=DisplaysChanged;ScalingChanged+=DisplaysChanged;
  copies.Changed+=()=>{if(selected=="clipboard"&&expanded)BuildCopies();};copies.Error+=ShowError;
  music.Changed+=UpdateMusic;camera.Changed+=UpdateCamera;
  PropertyChanged+=(_,e)=>{if(e.Property==IsVisibleProperty&&!IsVisible){Collapse();camera.SetActive(false);}};
  Opened+=(_,_)=>{if(!fixture){InitTray();meter.Start();custom.Configure(prefs.Sources);music.Enable(prefs.Music);copies.Enable(prefs.ClipboardHistory);sample.Start();}Sample();ArrangeDock(true);};
  Closed+=(_,_)=>Dispose();
  ContextMenu=Menu();BuildRail();
 }
 bool Vertical=>edge is Edge.Left or Edge.Right||edge==Edge.Floating&&prefs.DockVertical;
 bool HasSong=>prefs.Music&&prefs.MusicTitle&&music.Track.Player.Length>0;
 double RailLength=>Math.Max(120,prefs.Layout.Length*56+24+(prefs.CounterAbove?76:0)+(HasSong?108:0));
 ContextMenu Menu(){
  var menu=new ContextMenu();var items=new List<Control>();
  foreach(var value in Enum.GetValues<Edge>()){var item=new MenuItem{Header=value.ToString()};item.Click+=(_,_)=>SetEdge(value);items.Add(item);}
  items.Add(new Separator());
  void Add(string name,Action action){var item=new MenuItem{Header=name};item.Click+=(_,_)=>action();items.Add(item);}
  Add("Customize",()=>ShowModule("settings"));Add("Reset counters",()=>{meter.Reset();custom.Reset();Sample();});Add("Reduce motion",()=>{reduced=!reduced;prefs.ReduceMotion=reduced;BuildRail();SavePrefs();});Add("Hide",Hide);Add("Quit",Quit);menu.ItemsSource=items;return menu;
 }
 void InitTray(){
  var menu=new NativeMenu();void Add(string name,Action action){var item=new NativeMenuItem(name);item.Click+=(_,_)=>action();menu.Items.Add(item);}
  Add("Show TokenDock",()=>{Show();Activate();});Add("Customize",()=>{Show();ShowModule("settings");});Add("Quit",Quit);
  tray=new TrayIcon{Icon=new WindowIcon(Path.Combine(AppFiles.Resources,"assets","tokendock.ico")),ToolTipText="TokenDock",Menu=menu,IsVisible=true};tray.Clicked+=(_,_)=>{Show();Activate();};TrayIcon.SetIcons(Application.Current!,new TrayIcons{tray});
 }
 void Quit(){Dispose();if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)desktop.Shutdown();}
 internal void SetEdge(Edge value){edge=value;prefs.DockEdge=value.ToString();if(value!=Edge.Floating)prefs.DockVertical=value is Edge.Left or Edge.Right;floating=Position;RememberPlacement();BuildRail();ArrangeDock(true);}
 void BuildRail(){
  icons.Children.Clear();buttons.Clear();counts.Clear();icons.Orientation=Vertical?Orientation.Vertical:Orientation.Horizontal;icons.Margin=new Thickness(12);icons.Spacing=12;icons.HorizontalAlignment=HorizontalAlignment.Center;icons.VerticalAlignment=VerticalAlignment.Center;featured=null;if(prefs.CounterAbove){featured=Ui.Text("0",13,Ui.Mint);featured.Width=64;featured.VerticalAlignment=VerticalAlignment.Center;icons.Children.Add(featured);}
  foreach(var id in prefs.Layout){
   Control icon=id=="cat"?(cat=new Cat(prefs)):Ui.Icon(id);
   var button=Ui.Button(icon,()=>ShowModule(id),Label(id));button.Width=44;button.Height=44;button.Padding=new Thickness(7);button.Background=Brushes.Transparent;button.Resources["ButtonBackgroundPointerOver"]=Brushes.Transparent;button.Resources["ButtonBackgroundPressed"]=Brushes.Transparent;
   button.PointerEntered+=(_,_)=>{close.Stop();if(!reordering&&!dragging){pending=id;hover.Stop();hover.Start();}};
   button.GotFocus+=(_,_)=>ShowModule(id);
   button.KeyDown+=(_,e)=>{if(e.KeyModifiers.HasFlag(KeyModifiers.Alt)&&e.Key is Key.Left or Key.Right or Key.Up or Key.Down){var order=prefs.Layout;int from=Array.IndexOf(order,id),to=Math.Clamp(from+(e.Key is Key.Left or Key.Up?-1:1),0,order.Length-1);if(to!=from){prefs.MoveItem(id,order[to],to>from);SavePrefs();BuildRail();buttons[id].Focus();}e.Handled=true;}};
   button.PointerPressed+=(s,e)=>{if(e.GetCurrentPoint(this).Properties.IsLeftButtonPressed){press=e.GetPosition(this);pressedId=id;e.Pointer.Capture(button);e.Handled=true;}};
   button.PointerMoved+=(s,e)=>{
    if(pressedId!=id)return;var p=e.GetPosition(this);if(((Vector)(p-press)).Length>6)reordering=true;
    if(reordering){hover.Stop();close.Stop();button.Opacity=.55;}
   };
   button.PointerReleased+=(s,e)=>{
    if(pressedId!=id)return;e.Pointer.Capture(null);button.Opacity=1;pressedId="";
    if(reordering){reordering=false;var p=e.GetPosition(icons);string? target=null;foreach(var pair in buttons){var b=pair.Value.Bounds;if(b.Contains(p))target=pair.Key;}if(target!=null&&target!=id){prefs.MoveItem(id,target);SavePrefs();BuildRail();}e.Handled=true;}
    else ShowModule(id);
   };
   if(prefs.Ids.Contains(id)){var count=Ui.Text("0",10,Ui.Muted);count.HorizontalAlignment=HorizontalAlignment.Center;counts[id]=count;button.Content=null;var column=Ui.Stack(icon,count);column.Spacing=0;button.Content=column;button.Padding=new Thickness(0);}
   buttons[id]=button;icons.Children.Add(button);if(id=="music")SetMusicButton(button);
  }
 }
 string Label(string id)=>id switch{"music"=>"Music","clipboard"=>"Clipboard","camera"=>"Camera","cat"=>"RAM",_=>prefs.Name(id)};
 PixelRect[]? fixtureAreas;double fixtureScale=1;Avalonia.Platform.Screen? selectedScreen;PixelRect? previousArea;
 // macOS desktop coordinates are Cocoa points (Screen.Scaling == 1), even
 // when RenderScaling is 2 on Retina. Do not mix render pixels with screen space.
 double Scale=>fixture?fixtureScale:selectedScreen?.Scaling??Screens.ScreenFromPoint(anchor)?.Scaling??1;
 PixelRect WorkArea {
  get {
   if(fixtureAreas!=null)return DockPlacement.SelectArea(fixtureAreas,anchor);
   var screens=Screens.All;
   if(!dragging&&selectedScreen!=null){
    var same=screens.FirstOrDefault(s=>s.Equals(selectedScreen));
    if(same==null&&!string.IsNullOrEmpty(selectedScreen.DisplayName)){var named=screens.Where(s=>s.DisplayName==selectedScreen.DisplayName).ToArray();if(named.Length==1)same=named[0];}
    if(same!=null){selectedScreen=same;return same.WorkingArea;}
   }
   var area=DockPlacement.SelectArea(screens.Select(s=>s.WorkingArea),anchor);
   if(!dragging)selectedScreen=screens.FirstOrDefault(s=>s.WorkingArea==area);
   return area;
  }
 }
 internal void FixtureDisplays(PixelRect[] areas,PixelPoint point,double scale){if(!fixture)throw new InvalidOperationException("Synthetic displays require fixture mode.");fixtureAreas=areas;fixtureScale=scale;anchor=point;DisplaysChanged(this,EventArgs.Empty);}

 void DisplaysChanged(object? sender,EventArgs e){if(disposed||dragging)return;var area=WorkArea;if(edge==Edge.Floating&&previousArea is {} old)floating=new PixelPoint(area.X+floating.X-old.X,area.Y+floating.Y-old.Y);anchor=DockPlacement.Center(area);floating=DockPlacement.Locate(Edge.Floating,area,new PixelSize((int)(Width*Scale),(int)(Height*Scale)),floating);ArrangeDock(true);RememberPlacement();}
 void RememberPlacement(){prefs.DockEdge=edge.ToString();var point=edge==Edge.Floating?floating:anchor;prefs.DockX=point.X;prefs.DockY=point.Y;SavePrefs();}
 void BeginMove(object? sender,PointerPressedEventArgs e){if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;var pointer=this.PointToScreen(e.GetPosition(this));dragOffset=new PixelPoint(pointer.X-Position.X,pointer.Y-Position.Y);dragging=true;selectedScreen=null;prefs.DockVertical=Vertical;edge=Edge.Floating;floating=Position;anchor=pointer;e.Pointer.Capture(rail);ArrangeDock(true);}
 void Move(object? sender,PointerEventArgs e){if(!dragging)return;anchor=this.PointToScreen(e.GetPosition(this));floating=new PixelPoint(anchor.X-dragOffset.X,anchor.Y-dragOffset.Y);Position=floating;}
 void EndMove(object? sender,PointerReleasedEventArgs e){if(!dragging)return;dragging=false;e.Pointer.Capture(null);floating=Position;var area=WorkArea;edge=DockPlacement.Snap(Position,new PixelSize((int)(Width*Scale),(int)(Height*Scale)),area,Scale);anchor=DockPlacement.Center(area);RememberPlacement();BuildRail();ArrangeDock(true);}
 internal void ShowModule(string id){
  if(disposed||(shelfDragging&&id!="clipboard"))return;close.Stop();hover.Stop();if(selected==id&&expanded)return;
  camera.SetActive(false);selected=id;expanded=true;panel.IsVisible=true;ClearTransient();
  counter=null;details=null;ramNumber=null;ramBar=null;ramPercent=null;musicImage=null;musicTitle=null;musicArtist=null;musicTime=null;seek=null;play=null;cameraImage=null;cameraStatus=null;
  Control body=id switch{"music"=>BuildMusic(),"clipboard"=>copyCards,"camera"=>BuildCamera(),"cat"=>BuildRam(),"settings"=>new SettingsPane(prefs,ApplySettings,()=>ShowModule("all")),_=>BuildTokens(id)};
  content.Content=body;
  body.Opacity=reduced?1:.75;body.Transitions=new Transitions{new DoubleTransition{Property=OpacityProperty,Duration=TimeSpan.FromMilliseconds(reduced?0:100)}};
  if(id=="clipboard")BuildCopies();else ResizeBody();
  if(id=="camera"&&!fixture)camera.SetActive(true);
  SampleContent();ArrangeDock();Dispatcher.UIThread.Post(()=>{if(!disposed&&expanded){if(ReferenceEquals(content.Content,body))body.Opacity=1;ResizeBody();}},DispatcherPriority.Loaded);
 }
 internal void Collapse(){if(!expanded)return;expanded=false;hover.Stop();camera.SetActive(false);ArrangeDock();}
 void ResizeBody(){var body=content.Content as Control;if(body==null)return;body.Measure(new Size(304,double.PositiveInfinity));targetBody=Math.Clamp(body.DesiredSize.Height+48,140,520);bodyHeight.Target=targetBody;ArrangeDock();}
 void ArrangeDock(bool immediate=false){
  double length=RailLength;double p=expanded?352:0;
  width.Target=Vertical?72+p:Math.Max(length,p);height.Target=Vertical?Math.Max(length,expanded?targetBody:0):72+(expanded?targetBody:0);
  if(immediate){width.Value=width.Target;height.Value=height.Target;bodyHeight.Value=bodyHeight.Target;}
  last=clock.Elapsed.TotalSeconds;motion.Start();Animate();
 }
 void Animate(){
  double now=clock.Elapsed.TotalSeconds,dt=now-last;last=now;bool moving=width.Step(dt,reduced)|height.Step(dt,reduced)|bodyHeight.Step(dt,reduced);
  var area=WorkArea;previousArea=area;Width=Math.Min(area.Width/Scale,Math.Max(72,width.Value));Height=Math.Min(area.Height/Scale,Math.Max(72,height.Value));
  if(Vertical){rail.Width=72;rail.Height=Math.Min(RailLength,Height);Canvas.SetLeft(rail,edge==Edge.Right?Width-72:0);Canvas.SetTop(rail,(Height-rail.Height)/2);panel.Width=Math.Max(0,Width-72);panel.Height=Math.Min(Height,bodyHeight.Value);Canvas.SetLeft(panel,edge==Edge.Right?0:72);Canvas.SetTop(panel,(Height-panel.Height)/2);}
  else{rail.Width=Math.Min(RailLength,Width);rail.Height=72;Canvas.SetLeft(rail,(Width-rail.Width)/2);Canvas.SetTop(rail,edge==Edge.Bottom?Height-72:0);panel.Width=Width;panel.Height=Math.Max(0,Height-72);Canvas.SetLeft(panel,0);Canvas.SetTop(panel,edge==Edge.Bottom?0:72);}
  rail.CornerRadius=edge switch{Edge.Top=>new(0,0,28,28),Edge.Bottom=>new(28,28,0,0),Edge.Left=>new(0,28,28,0),Edge.Right=>new(28,0,0,28),_=>new(28)};
  panel.CornerRadius=new CornerRadius(28);panel.IsVisible=expanded||moving;
  if(!dragging){Position=DockPlacement.Locate(edge,area,new PixelSize((int)(Width*Scale),(int)(Height*Scale)),floating);}
  if(!moving){motion.Stop();panel.IsVisible=expanded;}
 }
 Control BuildTokens(string id){counter=Ui.Text("0",42);details=Ui.Text("",13,Ui.Muted);if(!graphs.TryGetValue(id,out var graph))graphs[id]=graph=new TokenGraph();if(graph.Parent is StackPanel parent)parent.Children.Remove(graph);return Ui.Stack(Ui.Text(prefs.Name(id),14,Ui.Muted),counter,graph,details);}
 Control BuildRam(){ramNumber=Ui.Text("",42);ramBar=new ProgressBar{Height=5,Maximum=100,Foreground=Ui.Mint};ramPercent=Ui.Text("",13,Ui.Mint);return Ui.Stack(Ui.Text("Available RAM",14,Ui.Muted),ramNumber,ramBar,ramPercent);}
 Control BuildMusic(){musicImage=new Image{Width=100,Height=100,Stretch=Stretch.UniformToFill};musicTitle=Ui.Text("Nothing playing",22);musicArtist=Ui.Text("",13,Ui.Muted);musicTime=Ui.Text("",12,Ui.Muted);seek=new Slider{Minimum=0,Maximum=1,Height=20};Avalonia.Automation.AutomationProperties.SetName(seek,"Playback position");seek.KeyUp+=async(_,e)=>{if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown){await music.Command("position",seek.Value);e.Handled=true;}};seek.AddHandler(PointerReleasedEvent,async(_,_)=>await music.Command("position",seek.Value),Avalonia.Interactivity.RoutingStrategies.Bubble,true);
  var transport=new StackPanel{Orientation=Orientation.Horizontal,Spacing=16,HorizontalAlignment=HorizontalAlignment.Center};transport.Children.Add(Ui.Button(Ui.Transport("previous"),()=>_=music.Command("previous"),"Previous track"));play=Ui.Button(Ui.Transport("play"),()=>_=music.Command("play-pause"),"Play or pause");transport.Children.Add(play);transport.Children.Add(Ui.Button(Ui.Transport("next"),()=>_=music.Command("next"),"Next track"));
  UpdateMusic();return Ui.Stack(musicImage,musicTitle,musicArtist,seek,musicTime,transport);
 }
 void UpdateMusic(){
  if(musicTitle!=null)musicTitle.Text=music.Track.Title;if(musicArtist!=null)musicArtist.Text=music.Status.Length>0?music.Status:music.Track.Artist;if(play!=null)play.Content=Ui.Transport(music.Track.Playing?"pause":"play");
  if(musicImage!=null&&!ReferenceEquals(panelArt,music.Artwork)){panelArt=music.Artwork;var old=artBitmap;artBitmap=music.Artwork==null?null:new Bitmap(new MemoryStream(music.Artwork));musicImage.Source=artBitmap;musicImage.IsVisible=artBitmap!=null;old?.Dispose();}
  if(!ReferenceEquals(lastArt,music.Artwork)){var old=barArt;barArt=music.Artwork==null?null:new Bitmap(new MemoryStream(music.Artwork));lastArt=music.Artwork;if(buttons.TryGetValue("music",out var b))SetMusicButton(b);old?.Dispose();}
  if(buttons.TryGetValue("music",out var button))SetMusicButton(button);
  ArrangeDock();
  if(selected=="music"&&expanded)ResizeBody();
 }
 void SetMusicButton(Button button){
  Control icon=prefs.MusicPreview&&barArt!=null?new Image{Source=barArt,Width=30,Height=30}:Ui.Icon("music");
  button.Width=Vertical?44:HasSong?152:44;button.Height=Vertical&&HasSong?152:44;
  if(HasSong){var title=new TextBlock{Text=music.Track.Title,MaxWidth=96,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12,VerticalAlignment=VerticalAlignment.Center};Control label=Vertical?new LayoutTransformControl{LayoutTransform=new RotateTransform(90),Child=title}:title;var row=new StackPanel{Orientation=Vertical?Orientation.Vertical:Orientation.Horizontal,Spacing=8};row.Children.Add(icon);row.Children.Add(label);button.Content=row;}else button.Content=icon;
  ToolTip.SetTip(button,prefs.MusicTitle?music.Track.Title:"Music");
 }
 Control BuildCamera(){cameraImage=new Image{Width=224,Height=224,Stretch=Stretch.UniformToFill,Clip=new EllipseGeometry(new Rect(0,0,224,224))};cameraStatus=Ui.Text(camera.Status,12,Ui.Muted);
  var shutter=Ui.Button("●",async()=>{try{var photo=await camera.Photo();if(disposed)return;copies.Add(photo);ShowModule("clipboard");}catch(Exception e)when(e is IOException or UnauthorizedAccessException){ShowError(e.Message);}},"Take photo");shutter.FontSize=30;shutter.Width=62;shutter.Height=62;shutter.CornerRadius=new CornerRadius(31);shutter.BorderBrush=Ui.Mint;shutter.BorderThickness=new Thickness(2);shutter.HorizontalAlignment=HorizontalAlignment.Center;UpdateCamera();return Ui.Stack(cameraImage,shutter,cameraStatus);
 }
 void UpdateCamera(){bool statusChanged=cameraStatus!=null&&cameraStatus.Text!=camera.Status;if(cameraStatus!=null)cameraStatus.Text=camera.Status;if(cameraImage!=null){var old=frameBitmap;frameBitmap=camera.Frame?.Bitmap();cameraImage.Source=frameBitmap;old?.Dispose();}if(statusChanged&&selected=="camera"&&expanded)ResizeBody();}
 void BuildCopies(){
  copyCards.Children.Clear();foreach(var image in transient)image.Dispose();transient.Clear();var header=new DockPanel();header.Children.Add(Ui.Text($"Clipboard · {copies.History.Items.Count}",14,Ui.Mint));var clear=Ui.Button("Clear",()=>{copies.History.Clear();BuildCopies();});clear.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(clear);copyCards.Children.Add(header);
  var rows=new StackPanel{Spacing=8};foreach(var item in copies.History.Items){
   var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),ColumnSpacing=6};var preview=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
   if(item.Png!=null){var bitmap=new Bitmap(new MemoryStream(item.Png));transient.Add(bitmap);preview.Children.Add(new Image{Source=bitmap,Width=44,Height=44,Stretch=Stretch.Uniform});}
   preview.Children.Add(new TextBlock{Text=item.Preview,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=140,VerticalAlignment=VerticalAlignment.Center});row.Children.Add(preview);
   var copy=Ui.Button("Copy",()=>{},"Copy "+item.Preview);copy.Click+=async(_,_)=>{try{await copies.Copy(item);copy.Content="✓";}catch(Exception e)when(e is IOException or InvalidOperationException){ShowError("Could not copy this item.");}};Grid.SetColumn(copy,1);row.Children.Add(copy);
   var remove=Ui.Button("×",()=>{copies.History.Remove(item.Key);BuildCopies();},"Remove item");remove.Opacity=0;Grid.SetColumn(remove,2);row.Children.Add(remove);row.PointerEntered+=(_,_)=>remove.Opacity=1;row.PointerExited+=(_,_)=>remove.Opacity=remove.IsKeyboardFocusWithin?1:0;remove.GotFocus+=(_,_)=>remove.Opacity=1;
   Point start=default;PointerPressedEventArgs? pressed=null;preview.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(preview).Properties.IsLeftButtonPressed){start=e.GetPosition(preview);pressed=e;}};
   preview.PointerMoved+=async(_,e)=>{if(pressed==null||((Vector)(e.GetPosition(preview)-start)).Length<6)return;var trigger=pressed;pressed=null;close.Stop();hover.Stop();shelfDragging=true;try{using var data=await item.Transfer(this);await DragDrop.DoDragDropAsync(trigger,data,DragDropEffects.Copy);}catch(Exception error)when(error is IOException or InvalidOperationException){ShowError("Could not drag this item.");}finally{shelfDragging=false;if(!IsPointerOver)close.Start();}};preview.PointerReleased+=(_,_)=>pressed=null;
   rows.Children.Add(new Border{Child=row,Background=Ui.Card,CornerRadius=new CornerRadius(12),Padding=new Thickness(10)});
  }
  if(copies.History.Items.Count==0)rows.Children.Add(Ui.Text(prefs.ClipboardHistory?"Drop files here, or copy text and images.":"Drop files here. Enable clipboard history in Customize to collect copies.",14,Ui.Muted));
  copyCards.Children.Add(new ScrollViewer{Content=rows,MaxHeight=360});if(selected=="clipboard")ResizeBody();
 }
 void DragOver(object? sender,DragEventArgs e){if(e.DataTransfer.Contains(DataFormat.File)){e.DragEffects=DragDropEffects.Copy;close.Stop();ShowModule("clipboard");e.Handled=true;}else e.DragEffects=DragDropEffects.None;}
 async void Drop(object? sender,DragEventArgs e){var paths=e.DataTransfer.TryGetFiles()?.Select(f=>f.TryGetLocalPath()).Where(p=>p!=null).Cast<string>().ToArray()??[];if(paths.Length==0)return;e.Handled=true;close.Stop();hover.Stop();shelfDragging=true;try{await copies.Store(paths);}finally{shelfDragging=false;}if(!disposed)ShowModule("clipboard");}
 void ScheduleRead(){if(fixture||disposed||Interlocked.Exchange(ref refreshPending,1)!=0)return;Dispatcher.UIThread.Post(()=>{if(!disposed)live.Start();});}
 void Sample(){
  memory=fixture?new MemoryInfo(32UL*1024*1024*1024,11UL*1024*1024*1024):MemoryInfo.Read();meters=fixture?[new Meter("Codex"){Value=new(32000,2100,8000)},new Meter("Claude Code"){Value=new(18000,4000,2000)},new Meter("OpenCode")]:meter.Snapshot();cat?.Update(memory);if(featured!=null){var readings=meters.Concat(custom.Snapshot()).ToArray();var ids=Preferences.Builtin.Concat(prefs.Sources.Select(s=>s.Id)).ToArray();int i=Array.IndexOf(ids,prefs.Counter);long total=prefs.Counter=="all"?readings.Sum(m=>m.Value.Total):i>=0&&i<readings.Length?readings[i].Value.Total:0;featured.Text=total>=1000000?$"{total/1000000d:0.#}m":total>=1000?$"{total/1000d:0.#}k":total.ToString();}SampleContent();
  if(++tick%5==0&&selected=="cat"&&expanded&&!fixture)_=ReadConsumers();
 }
 async Task ReadConsumers(){var top=await Task.Run(()=>{var rows=new List<(string,long)>();foreach(var process in Process.GetProcesses())using(process)try{rows.Add((process.ProcessName,process.WorkingSet64));}catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}return rows.GroupBy(p=>p.Item1).Select(g=>(Name:g.Key,Bytes:g.Sum(p=>p.Item2))).OrderByDescending(p=>p.Bytes).Take(3).ToArray();});if(!disposed&&ramPercent!=null)ramPercent.Text=$"{memory.FreeRatio:P0} available\n"+string.Join(" · ",top.Select(p=>$"{p.Name} {p.Bytes/1048576} MB"));}
 void SampleContent(){
  var readings=meters.Concat(custom.Snapshot()).ToArray();var keys=Preferences.Builtin.Concat(prefs.Sources.Select(s=>s.Id)).ToArray();for(int i=0;i<readings.Length&&i<keys.Length;i++)if(counts.TryGetValue(keys[i],out var label))label.Text=readings[i].Value.Total>=1000?$"{readings[i].Value.Total/1000d:0.#}k":readings[i].Value.Total.ToString();
  if(counter!=null){var all=meters.Concat(custom.Snapshot()).ToArray();var ids=Preferences.Builtin.Concat(prefs.Sources.Select(s=>s.Id)).ToArray();Usage usage=new();if(selected=="all")foreach(var m in all)usage+=m.Value;else{int i=Array.IndexOf(ids,selected);if(i>=0&&i<all.Length)usage=all[i].Value;}counter.Text=usage.Total.ToString("N0");if(details!=null)details.Text=$"Input  {usage.Input:N0}     Output  {usage.Output:N0}     Cache  {usage.Cache:N0}";if(graphs.TryGetValue(selected,out var graph))graph.Add(usage.Total);}
  if(ramNumber!=null)ramNumber.Text=memory.Total==0?"Unavailable":$"{memory.Available/1073741824d:0.0} GB";if(ramBar!=null)ramBar.Value=memory.FreeRatio*100;if(ramPercent!=null&&tick%5==0)ramPercent.Text=$"{memory.FreeRatio:P0} available";
  if(seek!=null&&!seek.IsPointerOver&&!seek.IsKeyboardFocusWithin){seek.Maximum=Math.Max(1,music.Track.Length);seek.Value=music.Position;}if(musicTime!=null)musicTime.Text=$@"{TimeSpan.FromSeconds(music.Position):m\:ss} / {TimeSpan.FromSeconds(music.Track.Length):m\:ss}";
 }
 void ApplySettings(Preferences value){prefs=value;prefs.Normalize();reduced=prefs.ReduceMotion;SavePrefs();BuildRail();if(!fixture){custom.Configure(prefs.Sources);music.Enable(prefs.Music);copies.Enable(prefs.ClipboardHistory);}ShowModule("all");}
 void SavePrefs(){if(fixture)return;try{prefs.Save();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){ShowError("Could not save preferences.");}}
 void ShowError(string message){if(content.Content is StackPanel stack){if(errorLabel?.Parent==stack)errorLabel.Text=message;else{errorLabel=Ui.Text(message,13,Ui.Muted);stack.Children.Add(errorLabel);}ResizeBody();}}
 void ClearTransient(){foreach(var bitmap in transient)bitmap.Dispose();transient.Clear();frameBitmap?.Dispose();frameBitmap=null;artBitmap?.Dispose();artBitmap=null;panelArt=null;}
 public void Dispose(){if(disposed)return;disposed=true;Screens.Changed-=DisplaysChanged;ScalingChanged-=DisplaysChanged;live.Stop();sample.Stop();motion.Stop();hover.Stop();close.Stop();camera.Dispose();copies.Dispose();music.Dispose();meter.Dispose();custom.Dispose();tray?.Dispose();barArt?.Dispose();ClearTransient();}
 internal void Settle(){content.UpdateLayout();ResizeBody();width.Value=width.Target;height.Value=height.Target;bodyHeight.Value=bodyHeight.Target;width.Velocity=height.Velocity=bodyHeight.Velocity=0;Animate();content.UpdateLayout();}
 internal string Selected=>selected;internal bool Expanded=>expanded;
}
