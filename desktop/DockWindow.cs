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
 readonly Copies copies;readonly List<Bitmap> transient=[];readonly Dictionary<string,Button> buttons=[];readonly Dictionary<string,TokenGraph> graphs=[];
 Preferences prefs;Cat? cat;Edge edge=Edge.Top;bool expanded,disposed,dragging,reordering,reduced;string selected="all",pending="all",pressedId="";
 Point press;PixelPoint origin,floating;TrayIcon? tray;MemoryInfo memory;Meter[] meters=[];
 TextBlock? counter,details,ramNumber,ramPercent,musicTitle,musicArtist,musicTime,cameraStatus,errorLabel;Slider? seek;ProgressBar? ramBar;Image? cameraImage,musicImage;Button? play;Bitmap? frameBitmap,artBitmap,barArt;byte[]? lastArt;
 readonly StackPanel copyCards=new(){Spacing=8};double targetBody=240;int tick;
 public DockWindow(bool fixture=false){
  this.fixture=fixture;prefs=fixture?new Preferences{Music=true,ClipboardHistory=true,Camera=true}:Preferences.Load();prefs.Normalize();camera=new Camera();copies=new Copies(this);
  Title="TokenDock";WindowDecorations=WindowDecorations.None;CanResize=false;ShowInTaskbar=false;Topmost=true;Background=Brushes.Transparent;TransparencyLevelHint=[WindowTransparencyLevel.Transparent];
  Width=440;Height=72;Content=canvas;panel.Child=content;canvas.Children.Add(panel);canvas.Children.Add(rail);rail.Child=icons;panel.IsVisible=false;
  panel.PointerEntered+=(_,_)=>close.Stop();rail.PointerEntered+=(_,_)=>close.Stop();PointerExited+=(_,_)=>{if(!dragging&&!reordering)close.Start();};close.Tick+=(_,_)=>{close.Stop();Collapse();};
  hover.Tick+=(_,_)=>{hover.Stop();ShowModule(pending);};motion.Tick+=(_,_)=>Animate();
  sample.Tick+=(_,_)=>Sample();
  rail.PointerPressed+=BeginMove;rail.PointerMoved+=Move;rail.PointerReleased+=EndMove;
  KeyDown+=(_,e)=>{if(e.Key==Key.Escape){Collapse();e.Handled=true;}};
  DragDrop.SetAllowDrop(this,true);AddHandler(DragDrop.DragOverEvent,DragOver);AddHandler(DragDrop.DropEvent,Drop);
  copies.Changed+=()=>{if(selected=="clipboard")BuildCopies();};copies.Error+=ShowError;
  music.Changed+=UpdateMusic;camera.Changed+=UpdateCamera;
  PropertyChanged+=(_,e)=>{if(e.Property==IsVisibleProperty&&!IsVisible){Collapse();camera.SetActive(false);}};
  Opened+=(_,_)=>{if(!fixture){InitTray();meter.Start();custom.Configure(prefs.Sources);music.Enable(prefs.Music);copies.Enable(prefs.ClipboardHistory);sample.Start();}Sample();ArrangeDock(true);};
  Closed+=(_,_)=>Dispose();
  ContextMenu=Menu();BuildRail();
 }
 bool Vertical=>edge is Edge.Left or Edge.Right;
 double RailLength=>Math.Max(120,prefs.Layout.Length*56+24+(prefs.CounterAbove?24:0));
 ContextMenu Menu(){
  var menu=new ContextMenu();var items=new List<Control>();
  foreach(var value in Enum.GetValues<Edge>()){var item=new MenuItem{Header=value.ToString()};item.Click+=(_,_)=>SetEdge(value);items.Add(item);}
  items.Add(new Separator());
  void Add(string name,Action action){var item=new MenuItem{Header=name};item.Click+=(_,_)=>action();items.Add(item);}
  Add("Customize",()=>ShowModule("settings"));Add("Reset counters",()=>{meter.Reset();custom.Reset();Sample();});Add("Reduce motion",()=>{reduced=!reduced;});Add("Hide",Hide);Add("Quit",Quit);menu.ItemsSource=items;return menu;
 }
 void InitTray(){
  var menu=new NativeMenu();void Add(string name,Action action){var item=new NativeMenuItem(name);item.Click+=(_,_)=>action();menu.Items.Add(item);}
  Add("Show TokenDock",()=>{Show();Activate();});Add("Customize",()=>{Show();ShowModule("settings");});Add("Quit",Quit);
  tray=new TrayIcon{Icon=new WindowIcon(Path.Combine(AppContext.BaseDirectory,"assets","tokendock.ico")),ToolTipText="TokenDock",Menu=menu,IsVisible=true};tray.Clicked+=(_,_)=>{Show();Activate();};TrayIcon.SetIcons(Application.Current!,new TrayIcons{tray});
 }
 void Quit(){Dispose();if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)desktop.Shutdown();}
 internal void SetEdge(Edge value){edge=value;floating=Position;BuildRail();ArrangeDock(true);}
 void BuildRail(){
  icons.Children.Clear();buttons.Clear();icons.Orientation=Vertical?Orientation.Vertical:Orientation.Horizontal;icons.Margin=new Thickness(12);icons.Spacing=12;icons.HorizontalAlignment=HorizontalAlignment.Center;icons.VerticalAlignment=VerticalAlignment.Center;
  foreach(var id in prefs.Layout){
   Control icon=id=="cat"?(cat=new Cat(prefs)):Ui.Icon(id);
   var button=Ui.Button(icon,()=>ShowModule(id),Label(id));button.Width=44;button.Height=44;button.Padding=new Thickness(7);button.Background=Brushes.Transparent;
   button.PointerEntered+=(_,_)=>{close.Stop();if(!reordering&&!dragging){pending=id;hover.Stop();hover.Start();}};
   button.GotFocus+=(_,_)=>ShowModule(id);
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
   buttons[id]=button;icons.Children.Add(button);
  }
 }
 string Label(string id)=>id switch{"music"=>"Music","clipboard"=>"Clipboard","camera"=>"Camera","cat"=>"RAM",_=>prefs.Name(id)};
 void BeginMove(object? sender,PointerPressedEventArgs e){if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;press=e.GetPosition(this);origin=Position;dragging=true;edge=Edge.Floating;floating=Position;e.Pointer.Capture(rail);ArrangeDock(true);}
 void Move(object? sender,PointerEventArgs e){if(!dragging)return;var delta=e.GetPosition(this)-press;floating=new PixelPoint(origin.X+(int)(delta.X*RenderScaling),origin.Y+(int)(delta.Y*RenderScaling));Position=floating;origin=Position;}
 void EndMove(object? sender,PointerReleasedEventArgs e){if(!dragging)return;dragging=false;e.Pointer.Capture(null);floating=Position;var screen=Screens.ScreenFromWindow(this)?.WorkingArea;if(screen is {} a){int sizeW=(int)(Width*RenderScaling),sizeH=(int)(Height*RenderScaling);if(Math.Abs(Position.X-a.X)<20)edge=Edge.Left;else if(Math.Abs(Position.X+sizeW-a.Right)<20)edge=Edge.Right;else if(Math.Abs(Position.Y-a.Y)<20)edge=Edge.Top;else if(Math.Abs(Position.Y+sizeH-a.Bottom)<20)edge=Edge.Bottom;}BuildRail();ArrangeDock(true);}
 internal void ShowModule(string id){
  if(disposed)return;close.Stop();hover.Stop();if(selected==id&&expanded)return;
  camera.SetActive(false);selected=id;expanded=true;panel.IsVisible=true;ClearTransient();
  counter=null;details=null;ramNumber=null;ramBar=null;ramPercent=null;musicImage=null;musicTitle=null;musicArtist=null;musicTime=null;seek=null;play=null;cameraImage=null;cameraStatus=null;
  Control body=id switch{"music"=>BuildMusic(),"clipboard"=>copyCards,"camera"=>BuildCamera(),"cat"=>BuildRam(),"settings"=>new SettingsPane(prefs,ApplySettings,()=>ShowModule("all")),_=>BuildTokens(id)};
  content.Content=body;
  body.Transitions=new Transitions{new DoubleTransition{Property=OpacityProperty,Duration=TimeSpan.FromMilliseconds(reduced?0:100)}};
  if(id=="clipboard")BuildCopies();else ResizeBody();
  if(id=="camera"&&!fixture)camera.SetActive(true);
  SampleContent();ArrangeDock();Dispatcher.UIThread.Post(()=>{if(!disposed&&expanded)ResizeBody();},DispatcherPriority.Loaded);
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
  Width=Math.Max(72,width.Value);Height=Math.Max(72,height.Value);
  if(Vertical){rail.Width=72;rail.Height=RailLength;Canvas.SetLeft(rail,edge==Edge.Right?Width-72:0);Canvas.SetTop(rail,(Height-rail.Height)/2);panel.Width=352;panel.Height=bodyHeight.Value;Canvas.SetLeft(panel,edge==Edge.Right?0:72);Canvas.SetTop(panel,(Height-panel.Height)/2);}
  else{rail.Width=RailLength;rail.Height=72;Canvas.SetLeft(rail,(Width-rail.Width)/2);Canvas.SetTop(rail,edge==Edge.Bottom?Height-72:0);panel.Width=Width;panel.Height=Math.Max(0,Height-72);Canvas.SetLeft(panel,0);Canvas.SetTop(panel,edge==Edge.Bottom?0:72);}
  rail.CornerRadius=edge switch{Edge.Top=>new(0,0,28,28),Edge.Bottom=>new(28,28,0,0),Edge.Left=>new(0,28,28,0),Edge.Right=>new(28,0,0,28),_=>new(28)};
  panel.CornerRadius=new CornerRadius(28);panel.IsVisible=expanded||moving;
  if(!dragging){var area=Screens.ScreenFromWindow(this)?.WorkingArea??new PixelRect(0,0,1920,1080);Position=DockPlacement.Locate(edge,area,new PixelSize((int)(Width*RenderScaling),(int)(Height*RenderScaling)),floating);}
  if(!moving){motion.Stop();panel.IsVisible=expanded;}
 }
 Control BuildTokens(string id){counter=Ui.Text("0",42);details=Ui.Text("",13,Ui.Muted);if(!graphs.TryGetValue(id,out var graph))graphs[id]=graph=new TokenGraph();if(graph.Parent is StackPanel parent)parent.Children.Remove(graph);return Ui.Stack(Ui.Text(prefs.Name(id),14,Ui.Muted),counter,graph,details);}
 Control BuildRam(){ramNumber=Ui.Text("",42);ramBar=new ProgressBar{Height=5,Maximum=100,Foreground=Ui.Mint};ramPercent=Ui.Text("",13,Ui.Mint);return Ui.Stack(Ui.Text("Available RAM",14,Ui.Muted),ramNumber,ramBar,ramPercent);}
 Control BuildMusic(){musicImage=new Image{Width=100,Height=100,Stretch=Stretch.UniformToFill};musicTitle=Ui.Text("Nothing playing",22);musicArtist=Ui.Text("",13,Ui.Muted);musicTime=Ui.Text("",12,Ui.Muted);seek=new Slider{Minimum=0,Maximum=1,Height=20};Avalonia.Automation.AutomationProperties.SetName(seek,"Playback position");seek.AddHandler(PointerReleasedEvent,async(_,_)=>await music.Command("position",seek.Value),Avalonia.Interactivity.RoutingStrategies.Bubble,true);
  var transport=new StackPanel{Orientation=Orientation.Horizontal,Spacing=16,HorizontalAlignment=HorizontalAlignment.Center};transport.Children.Add(Ui.Button("⏮",()=>_=music.Command("previous"),"Previous track"));play=Ui.Button("▶",()=>_=music.Command("play-pause"),"Play or pause");transport.Children.Add(play);transport.Children.Add(Ui.Button("⏭",()=>_=music.Command("next"),"Next track"));
  UpdateMusic();return Ui.Stack(musicImage,musicTitle,musicArtist,seek,musicTime,transport);
 }
 void UpdateMusic(){
  if(musicTitle!=null)musicTitle.Text=music.Track.Title;if(musicArtist!=null)musicArtist.Text=music.Status.Length>0?music.Status:music.Track.Artist;if(play!=null)play.Content=music.Track.Playing?"Ⅱ":"▶";
  if(musicImage!=null){var old=artBitmap;artBitmap=music.Artwork==null?null:new Bitmap(new MemoryStream(music.Artwork));musicImage.Source=artBitmap;old?.Dispose();}
  if(buttons.TryGetValue("music",out var button)){if(prefs.MusicPreview&&music.Artwork!=null){if(!ReferenceEquals(lastArt,music.Artwork)){var old=barArt;barArt=new Bitmap(new MemoryStream(music.Artwork));lastArt=music.Artwork;button.Content=new Image{Source=barArt,Width=30,Height=30};old?.Dispose();}ToolTip.SetTip(button,prefs.MusicTitle?music.Track.Title:"Music");}else button.Content=Ui.Icon("music");}
  if(selected=="music"&&expanded)ResizeBody();
 }
 Control BuildCamera(){cameraImage=new Image{Width=224,Height=224,Stretch=Stretch.UniformToFill,Clip=new EllipseGeometry(new Rect(0,0,224,224))};cameraStatus=Ui.Text(camera.Status,12,Ui.Muted);
  var shutter=Ui.Button("●",async()=>{try{var photo=await camera.Photo();copies.Add(photo);ShowModule("clipboard");}catch(Exception e)when(e is IOException or UnauthorizedAccessException){ShowError(e.Message);}},"Take photo");shutter.FontSize=30;shutter.HorizontalAlignment=HorizontalAlignment.Center;return Ui.Stack(cameraImage,shutter,cameraStatus);
 }
 void UpdateCamera(){if(cameraStatus!=null)cameraStatus.Text=camera.Status;if(cameraImage!=null){var old=frameBitmap;frameBitmap=camera.Frame?.Bitmap();cameraImage.Source=frameBitmap;old?.Dispose();}}
 void BuildCopies(){
  copyCards.Children.Clear();foreach(var image in transient)image.Dispose();transient.Clear();var header=new DockPanel();header.Children.Add(Ui.Text($"Clipboard · {copies.History.Items.Count}",14,Ui.Mint));var clear=Ui.Button("Clear",()=>{copies.History.Clear();BuildCopies();});clear.HorizontalAlignment=HorizontalAlignment.Right;header.Children.Add(clear);copyCards.Children.Add(header);
  var rows=new StackPanel{Spacing=8};foreach(var item in copies.History.Items){
   var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),ColumnSpacing=6};var preview=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
   if(item.Png!=null){var bitmap=new Bitmap(new MemoryStream(item.Png));transient.Add(bitmap);preview.Children.Add(new Image{Source=bitmap,Width=44,Height=44,Stretch=Stretch.Uniform});}
   preview.Children.Add(new TextBlock{Text=item.Preview,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=140,VerticalAlignment=VerticalAlignment.Center});row.Children.Add(preview);
   var copy=Ui.Button("Copy",()=>{},"Copy "+item.Preview);copy.Click+=async(_,_)=>{try{await copies.Copy(item);copy.Content="✓";}catch(Exception e)when(e is IOException or InvalidOperationException){ShowError("Could not copy this item.");}};Grid.SetColumn(copy,1);row.Children.Add(copy);
   var remove=Ui.Button("×",()=>{copies.History.Remove(item.Key);BuildCopies();},"Remove item");remove.Opacity=0;Grid.SetColumn(remove,2);row.Children.Add(remove);row.PointerEntered+=(_,_)=>remove.Opacity=1;row.PointerExited+=(_,_)=>remove.Opacity=remove.IsKeyboardFocusWithin?1:0;remove.GotFocus+=(_,_)=>remove.Opacity=1;
   Point start=default;PointerPressedEventArgs? pressed=null;preview.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(preview).Properties.IsLeftButtonPressed){start=e.GetPosition(preview);pressed=e;}};
   preview.PointerMoved+=async(_,e)=>{if(pressed==null||((Vector)(e.GetPosition(preview)-start)).Length<6)return;var trigger=pressed;pressed=null;close.Stop();try{using var data=await item.Transfer(this);await DragDrop.DoDragDropAsync(trigger,data,DragDropEffects.Copy);}catch(Exception error)when(error is IOException or InvalidOperationException){ShowError("Could not drag this item.");}};preview.PointerReleased+=(_,_)=>pressed=null;
   rows.Children.Add(new Border{Child=row,Background=Ui.Card,CornerRadius=new CornerRadius(12),Padding=new Thickness(10)});
  }
  if(copies.History.Items.Count==0)rows.Children.Add(Ui.Text(prefs.ClipboardHistory?"Drop files here, or copy text and images.":"Drop files here. Enable clipboard history in Customize to collect copies.",14,Ui.Muted));
  copyCards.Children.Add(new ScrollViewer{Content=rows,MaxHeight=360});if(selected=="clipboard")ResizeBody();
 }
 void DragOver(object? sender,DragEventArgs e){if(e.DataTransfer.Contains(DataFormat.File)){e.DragEffects=DragDropEffects.Copy;close.Stop();ShowModule("clipboard");e.Handled=true;}else e.DragEffects=DragDropEffects.None;}
 async void Drop(object? sender,DragEventArgs e){var paths=e.DataTransfer.TryGetFiles()?.Select(f=>f.TryGetLocalPath()).Where(p=>p!=null).Cast<string>().ToArray()??[];if(paths.Length==0)return;e.Handled=true;await copies.Store(paths);ShowModule("clipboard");}
 void Sample(){
  memory=fixture?new MemoryInfo(32UL*1024*1024*1024,11UL*1024*1024*1024):MemoryInfo.Read();meters=fixture?[new Meter("Codex"){Value=new(32000,2100,8000)},new Meter("Claude Code"){Value=new(18000,4000,2000)},new Meter("OpenCode")]:meter.Snapshot();cat?.Update(memory);SampleContent();
  if(++tick%5==0&&selected=="cat"&&expanded&&!fixture)_=ReadConsumers();
 }
 async Task ReadConsumers(){var top=await Task.Run(()=>{var rows=new List<(string,long)>();foreach(var process in Process.GetProcesses())using(process)try{rows.Add((process.ProcessName,process.WorkingSet64));}catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}return rows.GroupBy(p=>p.Item1).Select(g=>(Name:g.Key,Bytes:g.Sum(p=>p.Item2))).OrderByDescending(p=>p.Bytes).Take(3).ToArray();});if(!disposed&&ramPercent!=null)ramPercent.Text=$"{memory.FreeRatio:P0} available\n"+string.Join(" · ",top.Select(p=>$"{p.Name} {p.Bytes/1048576} MB"));}
 void SampleContent(){
  if(counter!=null){var all=meters.Concat(custom.Snapshot()).ToArray();var ids=Preferences.Builtin.Concat(prefs.Sources.Select(s=>s.Id)).ToArray();Usage usage=new();if(selected=="all")foreach(var m in all)usage+=m.Value;else{int i=Array.IndexOf(ids,selected);if(i>=0&&i<all.Length)usage=all[i].Value;}counter.Text=usage.Total.ToString("N0");if(details!=null)details.Text=$"Input  {usage.Input:N0}     Output  {usage.Output:N0}     Cache  {usage.Cache:N0}";if(graphs.TryGetValue(selected,out var graph))graph.Add(usage.Total);}
  if(ramNumber!=null)ramNumber.Text=$"{memory.Available/1073741824d:0.0} GB";if(ramBar!=null)ramBar.Value=memory.FreeRatio*100;if(ramPercent!=null&&tick%5==0)ramPercent.Text=$"{memory.FreeRatio:P0} available";
  if(seek!=null&&!seek.IsPointerOver){seek.Maximum=Math.Max(1,music.Track.Length);seek.Value=music.Position;}if(musicTime!=null)musicTime.Text=$@"{TimeSpan.FromSeconds(music.Position):m\:ss} / {TimeSpan.FromSeconds(music.Track.Length):m\:ss}";
 }
 void ApplySettings(Preferences value){prefs=value;prefs.Normalize();SavePrefs();BuildRail();if(!fixture){custom.Configure(prefs.Sources);music.Enable(prefs.Music);copies.Enable(prefs.ClipboardHistory);}ShowModule("all");}
 void SavePrefs(){if(fixture)return;try{prefs.Save();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){ShowError("Could not save preferences.");}}
 void ShowError(string message){errorLabel=Ui.Text(message,13,Ui.Muted);if(content.Content is StackPanel stack){stack.Children.Add(errorLabel);ResizeBody();}}
 void ClearTransient(){foreach(var bitmap in transient)bitmap.Dispose();transient.Clear();frameBitmap?.Dispose();frameBitmap=null;artBitmap?.Dispose();artBitmap=null;}
 public void Dispose(){if(disposed)return;disposed=true;sample.Stop();motion.Stop();hover.Stop();close.Stop();camera.Dispose();copies.Dispose();music.Dispose();meter.Dispose();custom.Dispose();tray?.Dispose();ClearTransient();}
 internal void Settle(){content.UpdateLayout();ResizeBody();width.Value=width.Target;height.Value=height.Target;bodyHeight.Value=bodyHeight.Target;width.Velocity=height.Velocity=bodyHeight.Velocity=0;Animate();content.UpdateLayout();}
 internal string Selected=>selected;internal bool Expanded=>expanded;
}
