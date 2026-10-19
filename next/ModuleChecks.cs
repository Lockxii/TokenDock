using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace TokenDock;

// Fixtures only: no desktop window, clipboard listener, media session or user preferences are opened.
static class ModuleChecks {
 public static void Run(Action<bool> check,string dir){
  var scratch=Path.Combine(Environment.CurrentDirectory,"work","module-checks-v13");Directory.CreateDirectory(scratch);
  var source=new CustomSource("custom","My tool",Path.Combine(scratch,"usage.json"));
  var p=new Preferences{Visible=["codex","bad","codex","custom"],Counter="bad",Sources=[source,source,new("bad","Bad","relative.json")]};p.Normalize();
  check(p.Visible.SequenceEqual(new[]{"codex","custom"})&&p.Counter=="all"&&p.Sources.Length==1);
  var copied=p.Copy();copied.Visible=[];check(p.Visible.Length==2);check(JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(p))!.Ids.SequenceEqual(p.Ids));
  var history=new CopyHistory();for(int i=0;i<25;i++)history.Add("Fixture "+i);check(history.Items.Count==20&&history.Items[0].Text=="Fixture 24"&&history.Items[^1].Text=="Fixture 5");
  history.Add("Fixture 8");check(history.Items.Count==20&&history.Items[0].Text=="Fixture 8"&&history.Items.Count(s=>s.Text=="Fixture 8")==1);
  history.Add(new string('x',32769));history.Add(" ");check(history.Items.Count==20);history.Clear();check(history.Items.Count==0);

  // The native readers emit usage events as soon as complete JSONL records arrive, without double counting.
  using(var monitor=new Monitor()){
   int events=0;monitor.Changed+=()=>events++;string path=Path.Combine(scratch,"codex.jsonl");
   string Codex(long input,long output,string timestamp)=>JsonSerializer.Serialize(new{timestamp,payload=new{type="token_count",info=new{total_token_usage=new{input_tokens=input,output_tokens=output,cached_input_tokens=20}}}})+"\n";
   string now=DateTime.UtcNow.AddMinutes(1).ToString("O");File.WriteAllText(path,Codex(100,50,DateTime.UtcNow.AddDays(-1).ToString("O")));monitor.ReadTail(path,0);check(monitor.Snapshot()[0].Value.Total==0&&events==0);
   string record=Codex(110,57,now);File.AppendAllText(path,record[..^2]);monitor.ReadTail(path,0);check(events==0);File.AppendAllText(path,record[^2..]);monitor.ReadTail(path,0);check(monitor.Snapshot()[0].Value.Total==17&&events==1);
   File.AppendAllText(path,record);monitor.ReadTail(path,0);check(monitor.Snapshot()[0].Value.Total==17&&events==1);
   File.AppendAllText(path,Codex(109,55,now)+Codex(111,59,now));monitor.ReadTail(path,0);check(monitor.Snapshot()[0].Value.Total==20&&events==2);
   monitor.Reset();File.AppendAllText(path,Codex(111,60,now));monitor.ReadTail(path,0);check(monitor.Snapshot()[0].Value.Total==1);
   string claudePath=Path.Combine(scratch,"claude.jsonl");string Claude(int output)=>JsonSerializer.Serialize(new{timestamp=now,message=new{id="fixture-message",usage=new{input_tokens=10,output_tokens=output}}})+"\n";
   File.WriteAllText(claudePath,Claude(1)+Claude(1)+Claude(3));monitor.ReadTail(claudePath,1);check(monitor.Snapshot()[1].Value.Total==13);
  }

  File.WriteAllText(source.Path,"{\"input\":100,\"output\":10,\"cache\":20}");
  using(var feeds=new CustomFeeds()){
   feeds.Configure([source]);check(feeds.Snapshot()[0].Value.Total==0);
   void Await(Func<bool> ready){check(SpinWait.SpinUntil(ready,3000));}
   File.WriteAllText(source.Path,"{\"input\":105,\"output\":12,\"cache\":20}");Await(()=>feeds.Snapshot()[0].Value.Total==7);
   foreach(string bad in new[]{"{","{\"input\":\"wrong\",\"output\":12}","{\"input\":1,\"output\":2,\"cache\":null}","{\"input\":-1,\"output\":2}","{\"input\":9223372036854775807,\"output\":2}"}){
    File.WriteAllText(source.Path,bad);Await(()=>feeds.Snapshot()[0].Status.StartsWith("Source unavailable"));check(feeds.Snapshot()[0].Value.Total==7);
    File.WriteAllText(source.Path,"{\"input\":105,\"output\":12,\"cache\":20}");Await(()=>feeds.Snapshot()[0].Status.StartsWith("Connected"));
   }
   var second=new CustomSource("second","Second",Path.Combine(scratch,"second.json"));File.WriteAllText(second.Path,"{\"input\":0,\"output\":0}");feeds.Configure([source,second]);check(feeds.Snapshot()[0].Value.Total==7);feeds.Configure([source]);check(feeds.Snapshot()[0].Value.Total==7);
   // Continuous writes must not postpone every read until the stream stops.
   using var flowing=new ManualResetEventSlim();Action onFlow=()=>{if(feeds.Snapshot().FirstOrDefault()?.Value.Total>7)flowing.Set();};feeds.Changed+=onFlow;
   var writer=Task.Run(()=>{for(int i=13;i<63;i++){File.WriteAllText(source.Path,JsonSerializer.Serialize(new{input=105,output=i,cache=20}));Thread.Sleep(20);}});
   check(flowing.Wait(800));check(!writer.IsCompleted);writer.GetAwaiter().GetResult();Await(()=>feeds.Snapshot()[0].Value.Total==57);
   feeds.Reset();File.WriteAllText(source.Path,"{\"input\":105,\"output\":63,\"cache\":20}");Await(()=>feeds.Snapshot()[0].Value.Total==1);feeds.Changed-=onFlow;
  }

  var memory=new MemoryInfo(32ul*1073741824,8ul*1073741824);var s=new NotchSurface{Running=false};s.SetFixture();s.Configure(new Preferences{Visible=["codex"]});check(s.OverallTotal==38200);
  int sampleCount=s.SamplesFor(0).Length;var meters=new[]{new Meter("Codex"){Value=new Usage(40000,500),Updated=DateTime.Now},new Meter("Claude Code"),new Meter("OpenCode")};
  s.Update(meters,memory,false);check(s.OverallTotal==40500&&s.SamplesFor(0).Length==sampleCount);s.Update(meters,memory);check(s.SamplesFor(0).Length==sampleCount+1);
  s.SetEdge("top");s.Open=0;s.SelectHover(new Point(85,24));check(s.Selected==0);s.SelectHover(new Point(550,24));check(s.Selected==-1);
  var full=new Preferences{CounterAbove=true,Counter="codex",Music=true,ClipboardHistory=true,Sources=[source,new("second","Autre",Path.Combine(scratch,"second.json")),new("third","Test",Path.Combine(scratch,"third.json"))]};full.Visible=full.Ids;
  s.Configure(full);s.Update(meters.Concat(full.Sources.Select(c=>new Meter(c.Name){Value=new Usage(120,45)})).ToArray(),memory,false);
  foreach(var side in new[]{"top","bottom","left","right"}){
   s.SetEdge(side);foreach(double open in new[]{0d,.5,1d}){s.Open=open;
    if(side is "left" or "right"){
     for(int i=0;i<6;i++){var r=s.VerticalRailIconBounds(i);check(new[]{r.TopLeft,r.TopRight,r.BottomLeft,r.BottomRight}.All(s.Contains));s.SelectHover(new Point(r.X+11,r.Y+11));check(s.Selected==i);}
     check(s.ActionAt(new Point(side=="right"?294:26,226))=="music");check(s.ActionAt(new Point(side=="right"?294:26,262))=="clipboard");
    }else{
     double x=50*open,y=(side=="bottom"?s.Height-66:0)+18+24;y=y*(1-open)+(side=="bottom"?s.Height-176+26:26)*open;
     check(s.ActionAt(new Point(504*(1-open)+(x+400)*open,y))=="music");check(s.ActionAt(new Point(555*(1-open)+(x+449)*open,y))=="clipboard");
    }
   }
   s.ClearHover();s.RenderFile(Path.Combine(dir,$"modules-{side}-rest.png"),false,side);s.RenderFile(Path.Combine(dir,$"modules-{side}-open.png"),true,side);
  }
  full.Visible=[];s.Configure(full);check(s.OverallTotal==0);s.RenderFile(Path.Combine(dir,"modules-empty.png"),true,"right");
  full.Visible=["claude","codex","opencode"];s.Configure(full);s.RenderFile(Path.Combine(dir,"modules-three.png"),false,"top");s.RenderFile(Path.Combine(dir,"modules-three-open.png"),true,"top");
  var view=new PersonalizationView(full,_=>{},()=>{},()=>meters);RenderPanel(view,Path.Combine(dir,"settings-preview.png"));check(view.ActualHeight>0);
  var scroll=new ScrollViewer{Content=new PersonalizationView(full,_=>{},()=>{},()=>meters),Height=520,Resources=PanelUi.Theme(),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};RenderPanel(scroll,Path.Combine(dir,"settings-scroll-preview.png"));check(scroll.ScrollableHeight>0);RenderPanel(scroll,Path.Combine(dir,"settings-scroll-bottom-preview.png"),true);check(scroll.VerticalOffset>0);
  using var clipboard=new ClipboardModule();using var clipboardView=new InlineModuleView(null,clipboard);RenderPanel(clipboardView,Path.Combine(dir,"clipboard-empty-preview.png"));check(clipboard.Status=="Disabled");
  using var music=new MusicModule(Dispatcher.CurrentDispatcher);using var musicView=new MusicView(music);RenderPanel(musicView,Path.Combine(dir,"music-empty-preview.png"));check(music.Status=="Disabled"&&!music.CanPlay);
  int actions=0;var menu=DockMenu.Create(()=>"top",_=>{},()=>{},()=>{},()=>{},()=>actions++,()=>actions++,()=>actions++);foreach(var item in menu.Items.OfType<MenuItem>().Where(m=>m.Header is string h&&h is "Customize…" or "Music" or "Clipboard"))item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));check(actions==3);
 }
 static void RenderPanel(FrameworkElement view,string file,bool scrollEnd=false){
  var border=new Border{Width=448,Background=new SolidColorBrush(Color.FromRgb(20,20,25)),Padding=new Thickness(20),Child=view};border.Measure(new Size(448,double.PositiveInfinity));border.Arrange(new Rect(border.DesiredSize));border.UpdateLayout();if(scrollEnd&&view is ScrollViewer scroll){scroll.ScrollToBottom();border.UpdateLayout();}var image=new RenderTargetBitmap(448,(int)Math.Ceiling(border.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(border);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(file);encoder.Save(stream);border.Child=null;
 }
}
