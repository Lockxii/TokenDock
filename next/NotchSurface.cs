using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Globalization;
namespace TokenDock;
sealed partial class NotchSurface:FrameworkElement {
 public static readonly DependencyProperty OpenProperty=DependencyProperty.Register("Open",typeof(double),typeof(NotchSurface),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender,null,(_,v)=>Math.Clamp((double)v,0,1)));
 public double Open{get=>(double)GetValue(OpenProperty);set=>SetValue(OpenProperty,value);}
 public static readonly DependencyProperty FloatingProperty=DependencyProperty.Register("Floating",typeof(double),typeof(NotchSurface),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender,null,(_,v)=>Math.Clamp((double)v,0,1)));
 public double Floating{get=>(double)GetValue(FloatingProperty);set=>SetValue(FloatingProperty,value);}
 public static readonly DependencyProperty BodyHeightProperty=DependencyProperty.Register("BodyHeight",typeof(double),typeof(NotchSurface),new FrameworkPropertyMetadata(100d,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsMeasure));
 internal double BodyHeight{get=>(double)GetValue(BodyHeightProperty);set=>SetValue(BodyHeightProperty,value);}
 double PanelDepth=>BodyHeight+76;
 SpringAnimation? openMotion,floatMotion,sizeMotion,shiftMotion;long openStart,floatStart,sizeStart,shiftStart,lastSwitch;bool floating;
 readonly ImageSource[] logos,cats,warmCats,hotCats;readonly Typeface normal=new("Segoe UI"),bold=new(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
 readonly DispatcherTimer catTimer=new();Queue<double>[] samples=[new(),new(),new()]; long[] previous=new long[3]; readonly Queue<DateTime> sampleTimes=new(); MemoryConsumer[] consumers=[]; Point? pointer;
 Meter[] data=[];MemoryInfo memory;string edge="top";int selected=-1,catFrame;bool running=true,ramDetail,expanded;
 public bool Running{get=>running;set{running=value;if(value)catTimer.Start();else catTimer.Stop();UpdateModuleTransform();}}
 bool Vertical=>edge is "left" or "right";
 static readonly Brush white=Solid(242,242,246),muted=Solid(140,140,155),mint=Solid(142,221,182),line=Solid(49,49,59),selection=Solid(24,25,31);
 Preferences preferences=new();int[] order=[1,0,2];
 string playingTitle="",playingArtist="";ImageSource? playingCover;bool playing,musicPreviewEntry;
 internal bool HasMusicPreview=>preferences.Music&&preferences.MusicPreview&&playing&&!string.IsNullOrWhiteSpace(playingTitle)&&playingTitle!="Nothing playing";
 internal Rect MusicPreviewBounds=>preferences.LayoutOrder!=null?ItemBounds("music"):Vertical?new(VerticalRailX-18,82,36,120):new(74,(edge=="bottom"?Height-ClosedDepth:0)+Extra+4,408,40);
 public void SetNowPlaying(MusicModule music){if(playing==music.Playing&&playingTitle==music.Title&&playingArtist==music.Artist&&ReferenceEquals(playingCover,music.Artwork))return;playing=music.Playing;playingTitle=music.Title;playingArtist=music.Artist;playingCover=music.Artwork;InvalidateVisual();}
 public bool WantsMusicPreview(Point point)=>HasMusicPreview&&Open<.5&&MusicPreviewBounds.Contains(point);
 public bool TryOpenMusicPreview(Point point,Func<FrameworkElement> view){if(!WantsMusicPreview(point))return false;if(ActiveModule!="music")ShowModule("music",view());musicPreviewEntry=true;return true;}
 readonly Border moduleHost=new(){Visibility=Visibility.Collapsed,AllowDrop=true,Background=Brushes.Transparent};
 public string? ActiveModule {get;private set;}
 static readonly DependencyProperty DetailFadeProperty=DependencyProperty.Register("DetailFade",typeof(double),typeof(NotchSurface),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
 static readonly DependencyProperty PanelShiftProperty=DependencyProperty.Register("PanelShift",typeof(double),typeof(NotchSurface),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsArrange));
 double PanelOffset=>BasePanelOffset+(double)GetValue(PanelShiftProperty);
 double BasePanelOffset=>preferences.LayoutOrder!=null&&ActiveModule=="clipboard"?Math.Clamp(ItemBounds("clipboard").Y+ItemBounds("clipboard").Height/2-(BodyHeight+118)/2,0,Math.Max(0,246-BodyHeight)):246-BodyHeight;
 internal Rect ModuleBounds=>Vertical?new(VerticalContentX,60+PanelOffset,200,BodyHeight):new((660-CurrentLength)/2+64,(edge=="bottom"?Height-PanelDepth:0)+58,448,BodyHeight);
 protected override int VisualChildrenCount=>1;
 protected override Visual GetVisualChild(int index)=>index==0?moduleHost:throw new ArgumentOutOfRangeException(nameof(index));
 protected override Size MeasureOverride(Size availableSize){moduleHost.Measure(ModuleBounds.Size);return new Size(Width,Height);}
 protected override Size ArrangeOverride(Size finalSize){moduleHost.Arrange(ModuleBounds);UpdateModuleTransform();return finalSize;}
 protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if(e.Property==OpenProperty||e.Property==FloatingProperty||e.Property==BodyHeightProperty||e.Property==PanelShiftProperty){if(moduleHost!=null)UpdateModuleTransform();InvalidateArrange();}}
 void UpdateModuleTransform(){
  if(ActiveModule==null)return;if(moduleHost.Child is CameraPane camera)camera.SetActive(running&&expanded&&Open>.95);if(moduleHost.Child is InlineModuleView view)view.SetActive(running&&Open>.95);Clip=CurrentShape();moduleHost.Opacity=Fade((Open-.3)/.7);moduleHost.IsHitTestVisible=Open>.95;
  var matrix=Vertical?VerticalContentMatrix:Matrix.Identity;var r=ModuleBounds;matrix.OffsetX+=(matrix.M11-1)*r.X;matrix.OffsetY+=(matrix.M22-1)*r.Y;moduleHost.RenderTransform=new MatrixTransform(matrix);
 }
 public void ShowModule(string kind,FrameworkElement view){
  double startOpacity=Stopwatch.GetElapsedTime(lastSwitch).TotalMilliseconds<300?Math.Max(.65,moduleHost.Child?.Opacity??(double)GetValue(DetailFadeProperty)):0;lastSwitch=Stopwatch.GetTimestamp();
  bool transition=running&&Open>.95&&SystemParameters.ClientAreaAnimation;double oldOffset=PanelOffset;
  // Replace directly: do not resize to the default body between two modules.
  if(moduleHost.Child is InlineModuleView previous)previous.DesiredHeightChanged-=ResizeModule;
  if(moduleHost.Child is IDisposable disposable)disposable.Dispose();
  BeginAnimation(PanelShiftProperty,null);SetValue(PanelShiftProperty,0d);ActiveModule=kind;moduleHost.Child=view;
  if(transition){
   if(Vertical){double shift=oldOffset-BasePanelOffset;SetValue(PanelShiftProperty,shift);Animate(PanelShiftProperty,0,ref shiftMotion,ref shiftStart,26);}
   view.Opacity=startOpacity;view.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(startOpacity,1,TimeSpan.FromMilliseconds(140)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
  }
  if(view is InlineModuleView inline)inline.DesiredHeightChanged+=ResizeModule;ResizeModule();moduleHost.Visibility=Visibility.Visible;ramDetail=false;selected=-1;UpdateModuleTransform();InvalidateMeasure();InvalidateVisual();
 }
 public void CloseModule(bool smooth=false){
  double startOpacity=Stopwatch.GetElapsedTime(lastSwitch).TotalMilliseconds<300?Math.Max(.65,moduleHost.Child?.Opacity??1):0;lastSwitch=Stopwatch.GetTimestamp();
  bool transition=smooth&&ActiveModule!=null&&running&&Open>.95&&SystemParameters.ClientAreaAnimation;double oldOffset=PanelOffset;
  BeginAnimation(PanelShiftProperty,null);SetValue(PanelShiftProperty,0d);if(moduleHost.Child is InlineModuleView inline)inline.DesiredHeightChanged-=ResizeModule;if(moduleHost.Child is IDisposable disposable)disposable.Dispose();moduleHost.Child=null;moduleHost.Visibility=Visibility.Collapsed;ActiveModule=null;musicPreviewEntry=false;Clip=null;
  BeginAnimation(DetailFadeProperty,null);SetValue(DetailFadeProperty,1d);
  if(transition){
   if(Vertical){double shift=oldOffset-BasePanelOffset;SetValue(PanelShiftProperty,shift);Animate(PanelShiftProperty,0,ref shiftMotion,ref shiftStart,26);}
   SetValue(DetailFadeProperty,startOpacity);BeginAnimation(DetailFadeProperty,new DoubleAnimation(startOpacity,1,TimeSpan.FromMilliseconds(140)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
  }
  ResizeModule();InvalidateMeasure();InvalidateVisual();
 }
 void ResizeModule(){if(IsIconDrag)return;double target=ActiveModule=="camera"?210:ActiveModule=="clipboard"&&moduleHost.Child is InlineModuleView view?view.DesiredBodyHeight:Vertical?246:100;if(!running){BeginAnimation(BodyHeightProperty,null);BodyHeight=target;sizeMotion=null;}else if(sizeMotion?.To!=target||sizeMotion==null&&Math.Abs(BodyHeight-target)>.01)Animate(BodyHeightProperty,target,ref sizeMotion,ref sizeStart,26);}
 internal string? HoverKey(Point point){foreach(var id in preferences.Layout){var bounds=VisibleItemBounds(id);if(bounds.IsEmpty)continue;bounds.Inflate(3,3);if(bounds.Contains(point))return id;}return null;}
 public bool TryOpenModule(Point point,Func<string,FrameworkElement> create){if(ActionAt(point) is not string kind)return false;if(ActiveModule!=kind)ShowModule(kind,create(kind));return true;}
 public bool IsModuleContent(Point point)=>ActiveModule!=null&&Open>.95&&ModuleBounds.Contains(point);
 int ModuleCount=>(preferences.Music?1:0)+(preferences.ClipboardHistory?1:0);
 double CatTop=>ModuleCount>0?294:274;
 double Slot=>408d/Math.Max(1,order.Length);double Extra=>preferences.CounterAbove?18:0;double ClosedDepth=>(preferences.LayoutOrder!=null?60:48)+Extra;
 public void Configure(Preferences value){var oldIds=preferences.Ids;var ids=value.Ids;if(!oldIds.SequenceEqual(ids)){var oldSamples=samples;var oldPrevious=previous;samples=ids.Select(id=>{int old=Array.IndexOf(oldIds,id);return old>=0&&old<oldSamples.Length?oldSamples[old]:new Queue<double>(Enumerable.Repeat(0d,sampleTimes.Count));}).ToArray();previous=ids.Select(id=>{int old=Array.IndexOf(oldIds,id);return old>=0&&old<oldPrevious.Length?oldPrevious[old]:0;}).ToArray();}preferences=value.Copy();preferences.Normalize();order=preferences.Visible.Select(id=>Array.IndexOf(ids,id)).Where(i=>i>=0).ToArray();selected=-1;ramDetail=false;InvalidateVisual();}
 int Pinned=>Array.IndexOf(preferences.Ids,preferences.Counter);
 public NotchSurface(){AddVisualChild(moduleHost);AddLogicalChild(moduleHost);logos=new[]{"claude","codex","opencode"}.Select(Logo).ToArray();cats=LoadCats();warmCats=cats.Select(c=>TintCat(c,Color.FromRgb(255,157,65))).ToArray();hotCats=cats.Select(c=>TintCat(c,Color.FromRgb(255,65,60))).ToArray();SetEdge("top");catTimer.Interval=TimeSpan.FromMilliseconds(120);catTimer.Tick+=(_,_)=>{catFrame=(catFrame+1)%cats.Length;InvalidateVisual();};catTimer.Start();Focusable=true;
  MouseLeave+=(_,_)=>ClearHover();
  KeyDown+=(_,e)=>{if(ActiveModule!=null&&e.OriginalSource!=this)return;if(e.Key is Key.Left or Key.Right){if(order.Length==0)return;CloseModule();int at=Array.IndexOf(order,selected);selected=order[(at+(e.Key==Key.Right?1:order.Length-1)+order.Length)%order.Length];ramDetail=false;InvalidateVisual();e.Handled=true;}};
 }
 static Brush Solid(byte r,byte g,byte b){var x=new SolidColorBrush(Color.FromRgb(r,g,b));x.Freeze();return x;}
 public void SetEdge(string side){BeginAnimation(PanelShiftProperty,null);SetValue(PanelShiftProperty,0d);edge=side;Width=Vertical?320:660;Height=Vertical?364:308;BeginAnimation(BodyHeightProperty,null);sizeMotion=null;BodyHeight=ActiveModule=="camera"?210:ActiveModule=="clipboard"&&moduleHost.Child is InlineModuleView v?v.DesiredBodyHeight:Vertical?246:100;UpdateModuleTransform();InvalidateMeasure();InvalidateVisual();}
 public void Expand(bool value){if(expanded==value)return;expanded=value;if(!value&&moduleHost.Child is CameraPane camera)camera.SetActive(false);Animate(OpenProperty,value?1:0,ref openMotion,ref openStart,Vertical?15:17);}
 public void SetFloating(bool value,bool animate=true){
  if(!animate){floating=value;BeginAnimation(FloatingProperty,null);Floating=value?1:0;floatMotion=null;return;}
  if(floating==value)return;floating=value;Animate(FloatingProperty,value?1:0,ref floatMotion,ref floatStart,20);
 }
 void Animate(DependencyProperty property,double target,ref SpringAnimation? previousMotion,ref long started,double frequency){
  double current=(double)GetValue(property),velocity=previousMotion?.Sample(Stopwatch.GetElapsedTime(started).TotalSeconds).Velocity??0;
  if(!SystemParameters.ClientAreaAnimation){BeginAnimation(property,null);SetValue(property,target);previousMotion=null;return;}
  var motion=new SpringAnimation{From=current,To=target,Velocity=velocity,Frequency=frequency,Duration=TimeSpan.FromSeconds(12/frequency)};
  previousMotion=motion;started=Stopwatch.GetTimestamp();BeginAnimation(property,motion,HandoffBehavior.SnapshotAndReplace);
 }
 static double Fade(double value){double t=Math.Clamp(value,0,1);return t*t*(3-2*t);}
 internal bool IsRamDetail=>ramDetail;
 public void ClearHover(){pointer=null;selected=-1;ramDetail=false;musicPreviewEntry=false;InvalidateVisual();}
 public void SelectHover(Point p){
  pointer=p;selected=-1;
  if(preferences.LayoutOrder!=null){bool inBody=Vertical?Local(p).X>54:p.Y-(edge=="bottom"?Height-PanelDepth:0)>49;var cat=ItemBounds("cat");bool catPath=!cat.IsEmpty&&(Vertical?Math.Abs(p.Y-(cat.Y+cat.Height/2))<30:Math.Abs(p.X-(cat.X+cat.Width/2))<30);if(!inBody&&!catPath)ramDetail=false;foreach(var id in preferences.Layout){var r=ItemBounds(id);r.Inflate(3,3);if(!r.Contains(p))continue;if(id=="cat")ramDetail=true;else {ramDetail=false;if(preferences.Ids.Contains(id))selected=Array.IndexOf(preferences.Ids,id);}break;}if(ActiveModule!=null&&(ramDetail||selected>=0))CloseModule(true);InvalidateVisual();return;}
  // Keep the player's entry path stable while the compact preview turns into its controls.
  if(WantsMusicPreview(p)||musicPreviewEntry){if(musicPreviewEntry&&Open>.95&&(Vertical?Local(p).X>54:p.Y>=49))musicPreviewEntry=false;ramDetail=false;InvalidateVisual();return;}
  // Header hit targets follow the actual upright drawing, including bottom docking.
  if(Vertical){var local=Local(p);
   if(local.X<54){ramDetail=local.Y>(ModuleCount>0?286:258);for(int i=0;i<order.Length;i++)if(Math.Abs(local.Y-(VerticalRailY(i)-4))<VerticalHitRadius){selected=order[i];ramDetail=false;}}
  }else if(Open<.5){ramDetail=p.X>=590;for(int i=0;i<order.Length;i++)if(p.X>=68+i*Slot&&p.X<68+(i+1)*Slot){selected=order[i];ramDetail=false;}}
  else {double x=p.X-(660-CurrentLength)/2,y=p.Y-(edge=="bottom"?Height-PanelDepth:0);
   if(y<49){ramDetail=x>455;for(int i=0;i<order.Length;i++)if(Math.Abs(x-(74+i*49))<23){selected=order[i];ramDetail=false;}}
   // Keep the RAM body inspectable after entering through the cat. Token bodies default to overall.
  }
  if(ActiveModule!=null&&(selected>=0||ramDetail)&&ActionAt(p)==null)CloseModule(true);InvalidateVisual();
 }
 public void SetConsumers(MemoryConsumer[] values){consumers=values;InvalidateVisual();}
 Usage UsageFor(int provider)=>provider<0?order.Aggregate(new Usage(),(sum,i)=>sum+(data.ElementAtOrDefault(i)?.Value??new Usage())):data.ElementAtOrDefault(provider)?.Value??new Usage();
 internal long OverallTotal=>UsageFor(-1).Total;
 internal double[] SamplesFor(int provider)=>provider>=0?samples[provider].ToArray():Enumerable.Range(0,samples[0].Count).Select(i=>order.Sum(p=>samples.ElementAtOrDefault(p)?.ElementAtOrDefault(i)??0)).ToArray();
 internal static int SampleIndex(Rect r,Point p,int count)=>count==0||!r.Contains(p)?-1:Math.Clamp((int)Math.Round((p.X-r.Left)/r.Width*(count-1)),0,count-1);
 public void Update(Meter[] values,MemoryInfo ram,bool takeSample=true){
  if(samples.Length!=values.Length){var old=samples;var totals=previous;samples=Enumerable.Range(0,values.Length).Select(i=>i<3&&i<old.Length?old[i]:new Queue<double>(Enumerable.Repeat(0d,sampleTimes.Count))).ToArray();previous=Enumerable.Range(0,values.Length).Select(i=>i<3&&i<totals.Length?totals[i]:0).ToArray();}
  if(takeSample){
  for(int i=0;i<values.Length;i++){long total=values[i].Value.Total;samples[i].Enqueue(Math.Max(0,total-previous[i]));previous[i]=total;while(samples[i].Count>60)samples[i].Dequeue();}
  sampleTimes.Enqueue(DateTime.Now);while(sampleTimes.Count>60)sampleTimes.Dequeue();}
  data=values;memory=ram;catTimer.Interval=TimeSpan.FromMilliseconds(Math.Clamp(1000/(cats.Length*ram.CyclesPerSecond*preferences.CatSpeed),30,800));
  AutomationProperties.SetName(this,string.Join(". ",data.Select(m=>$"{m.Name}, {m.Value.Total:N0} tokens, {m.Status}"))+". "+ram.AvailableText);InvalidateVisual();
 }
 double CurrentLength=>Vertical?364:660-100*Open;
 double CurrentDepth=>Vertical?52+268*Open:ClosedDepth+(PanelDepth-ClosedDepth)*Open;
 Matrix Matrix=>edge switch{"bottom"=>new(1,0,0,-1,0,Height),"left"=>new(0,1,1,0,0,0),"right"=>new(0,1,-1,0,Width,0),_=>Matrix.Identity};
 Point Local(Point p)=>edge switch{"bottom"=>new(p.X,Height-p.Y),"right"=>new(Width-p.X,p.Y),_=>p};
 internal Geometry CurrentShape(){
  double length=Vertical?364+(BodyHeight+118-364)*Open:CurrentLength;var g=Shape(length,CurrentDepth,Floating);var m=Matrix;if(!Vertical)m.OffsetX=(660-CurrentLength)/2;else m.OffsetY=PanelOffset*Open;g.Transform=new MatrixTransform(m);if(!Vertical)return g;
  var rail=Shape(364,52,Floating);rail.Transform=new MatrixTransform(Matrix);Geometry result=new CombinedGeometry(GeometryCombineMode.Union,g,rail);
  // Concave fillet where the shorter panel meets the fixed rail.
  void Join(double y,double radius,bool bottom){if(radius<=.01)return;double x=Width-52,sign=bottom?1:-1;var join=new StreamGeometry();using(var c=join.Open()){c.BeginFigure(new Point(x-radius,y),true,true);c.LineTo(new Point(x,y),true,false);c.LineTo(new Point(x,y+sign*radius),true,false);c.BezierTo(new Point(x,y+sign*radius*.45),new Point(x-radius*.45,y),new Point(x-radius,y),true,false);}if(edge=="left")join.Transform=new MatrixTransform(-1,0,0,1,Width,0);result=new CombinedGeometry(GeometryCombineMode.Union,result,join);}
  double offset=PanelOffset*Open,top=offset+(56-20*Math.Clamp((CurrentDepth-48)/128,0,1))*(1-Floating),bottom=offset+length-30*(1-Floating);Join(top,Math.Min(18*Open,Math.Max(0,Math.Min(offset,top-56)/2)),false);Join(bottom,Math.Min(18*Open,Math.Max(0,Math.Min(364-offset-length,310-bottom)/2)),true);

  return result;
 }

 protected override void OnRender(DrawingContext d){base.OnRender(d);var geometry=CurrentShape();d.DrawGeometry(Brushes.Black,null,geometry);d.PushClip(geometry);
  if(preferences.LayoutOrder!=null){DrawCustomRail(d);if(Vertical)DrawVertical(d);else if(Open>.001){d.PushOpacity(Math.Clamp((Open-.3)/.4,0,1));Hover(d,(660-CurrentLength)/2,edge=="bottom"?Height-PanelDepth:0);d.Pop();}DrawIconDrag(d);d.Pop();return;}
  if(Vertical){DrawVertical(d);DrawIconDrag(d);d.Pop();return;}
  // Content stays upright on the bottom edge; all transitions share one animation clock.
  double baseY=(edge=="bottom"?Height-ClosedDepth:0)+Extra;
  if(Open<.999){d.PushOpacity(Math.Clamp(1-Open/.35,0,1));Rest(d,baseY);d.Pop();}
  if(Open>.001){d.PushOpacity(Math.Clamp((Open-.3)/.4,0,1));Hover(d,(660-CurrentLength)/2,edge=="bottom"?Height-PanelDepth:0);d.Pop();}
  MorphHeader(d,baseY);Modules(d);DrawIconDrag(d);d.Pop();
 }
 void Rest(DrawingContext d,double y){
  if(preferences.CounterAbove)Text(d,$"{preferences.Name(preferences.Counter)}  ·  {UsageFor(Pinned).Total:N0} tokens",330,y-16,10,mint,true);
  if(HasMusicPreview){PreviewCover(d,new Rect(80,y+8,32,32));if(preferences.MusicTitle)Caption(d,playingTitle,124,y+7,346,12,white);if(preferences.MusicTitle)Caption(d,string.IsNullOrWhiteSpace(playingArtist)?"Playing":playingArtist,124,y+25,346,9.5,muted);return;}
  for(int i=0;i<order.Length;i++){double x=74+i*Slot;int provider=order[i];
   if(order.Length<=3){Caption(d,provider==1?"Claude":data.ElementAtOrDefault(provider)?.Name??preferences.Name(preferences.Ids[provider]),x+30,y+16,Math.Max(10,Slot-80),10.5,white);Text(d,Value(provider),x+Slot-20,y+15,11.5,white,false,true,true);}
   else Text(d,Value(provider),x+29,y+19,9,white);
   d.DrawLine(new Pen(line,1),new Point(x+Slot-12,y+14),new Point(x+Slot-12,y+34));
  }
 }
 void Hover(DrawingContext d,double x,double y){
  double offset=x;
  if(preferences.LayoutOrder==null){Caption(d,ActiveModule is string module?module=="music"?"Music":"Clipboard":ramDetail?"Memory":selected<0?"All tools":data.ElementAtOrDefault(selected)?.Name??"All tools",x+Math.Max(207,72+order.Length*49),y+20,Math.Max(24,390-Math.Max(207,72+order.Length*49)),10,muted);
  d.DrawLine(new Pen(line,1),new Point(x+Math.Max(191,56+order.Length*49),y+15),new Point(x+Math.Max(191,56+order.Length*49),y+36));if(ramDetail)d.DrawEllipse(memory.FreeRatio<.15?Solid(226,140,120):mint,null,new Point(x+522,y+26),2,2);}
  if(ActiveModule!=null)return;d.PushOpacity(Math.Clamp((Open-.52)/.4,0,1)*(double)GetValue(DetailFadeProperty));
  double mainY=y+62;
  if(ramDetail){Text(d,memory.Total==0?"—":$"{memory.Available/1073741824d:0.0}",x+64,mainY,40,white);Text(d,"GB available",x+64+Measure($"{memory.Available/1073741824d:0.0}",40)+12,mainY+22,15,muted);MemoryBar(d,new Rect(x+293,mainY+34,218,5));Text(d,$"Physical RAM · {memory.Total/1073741824d:0.0} GB",x+64,y+139,11,muted);Text(d,$"{memory.FreeRatio:P0} available",x+511,y+139,11,mint,false,false,true);Consumers(d,x+304,y+137);d.Pop();return;}
  string count=Value(selected);Text(d,count,x+64,mainY,40,white);double tw=Measure(count,40);Text(d,"tokens",x+64+tw+8,mainY+22,15,muted);Graph(d,new Rect(x+266,y+70,245,43),selected);
  Text(d,MeasureStatus(),x+266,y+119,9,muted);
  var usage=UsageFor(selected);string input=data.Length==0?"—":Compact(usage.Input+usage.Cache),output=data.Length==0?"—":Compact(usage.Output);
  Text(d,"Input",x+64,y+139,11.5,muted);Text(d,input,x+105,y+139,12,white);d.DrawLine(new Pen(line,1),new Point(x+169,y+140),new Point(x+169,y+154));Text(d,"Output",x+185,y+139,11.5,muted);Text(d,output,x+222,y+139,12,white);d.Pop();
 }
 void MorphHeader(DrawingContext d,double restY){double targetX=(660-CurrentLength)/2,targetY=edge=="bottom"?Height-PanelDepth:0;double Lerp(double a,double b)=>a+(b-a)*Open;
  if(HasMusicPreview)d.PushOpacity(Fade((Open-.25)/.3));
  for(int i=0;i<order.Length;i++){double x=Lerp(85+i*Slot,targetX+74+i*49),y=Lerp(restY+24,targetY+26);if(order[i]==selected&&!ramDetail){d.PushOpacity(Open);d.DrawRoundedRectangle(selection,null,new Rect(x-19,y-19,38,38),13,13);d.Pop();}DrawProvider(d,order[i],new Rect(x-11,y-11,22,22));}
  if(HasMusicPreview)d.Pop();
  Cat(d,new Rect(Lerp(588,targetX+472),Lerp(restY+10,targetY+8),Lerp(30,35),Lerp(29,34)));
 }
 double VerticalContentX=>edge=="right"?28:80;
 internal Rect VerticalGraphBounds=>new(VerticalContentX,166,200,50);
 internal Matrix VerticalContentMatrix=>new(Open,0,0,Open,(edge=="right"?Width-52:52)*(1-Open),Height/2*(1-Open));
 double VerticalLabelOffset=>ModuleCount>0&&preferences.CounterAbove?11:17;
 double VerticalHitRadius=>order.Length<2?28:Math.Min(28,Math.Abs(VerticalRailY(1)-VerticalRailY(0))/2);
 double VerticalRailX=>edge=="right"?Width-26:26;
 // Reserve the deepest closed shoulder before placing the fixed rail content.
 double VerticalRailY(int i){double first=preferences.CounterAbove?(ModuleCount>0?94:98):82;double last=ModuleCount>0?178:210;return first+i*(last-first)/Math.Max(1,order.Length-1);}
 internal Rect VerticalRailIconBounds(int i){double size=order.Length>3?(ModuleCount>0?16:18):22;return new Rect(VerticalRailX-size/2,VerticalRailY(i)-4-size/2,size,size);}
 internal IEnumerable<Rect> VerticalRailContentBounds(){
  for(int i=0;i<order.Length;i++){yield return VerticalRailIconBounds(i);if(order.Length>3)continue;var label=Format(Value(order[i]),10.5,white,false);yield return new Rect(VerticalRailX-label.Width/2,VerticalRailY(i)+VerticalLabelOffset,label.Width,label.Height);}
  yield return new Rect(VerticalRailX-17,CatTop,34,32);
 }
 void DrawVertical(DrawingContext d){double hx=VerticalRailX;if(preferences.LayoutOrder==null){if(preferences.CounterAbove){string count=UsageFor(Pinned).Total.ToString("N0",CultureInfo.GetCultureInfo("en-US"));Text(d,count,hx,63,Math.Min(10,420/Math.Max(1,Measure(count,10))),mint,true);}if(HasMusicPreview){d.PushOpacity(Math.Clamp(1-Open/.35,0,1));PreviewCover(d,new Rect(hx-18,82,36,36));d.PushTransform(new RotateTransform(90,hx,126));if(preferences.MusicTitle)Caption(d,playingTitle,hx,120,76,11,white);d.Pop();d.Pop();d.PushOpacity(Fade((Open-.25)/.3));}for(int i=0;i<order.Length;i++){DrawProvider(d,order[i],VerticalRailIconBounds(i));if(order.Length<=3)Text(d,Value(order[i]),hx,VerticalRailY(i)+VerticalLabelOffset,10.5,white,true);}if(HasMusicPreview)d.Pop();Modules(d);
  Cat(d,new Rect(hx-17,CatTop,34,32));}
  if(Open<.01||ActiveModule!=null)return;d.PushOpacity(Fade((Open-.3)/.7)*(double)GetValue(DetailFadeProperty));d.PushTransform(new MatrixTransform(VerticalContentMatrix));double x=VerticalContentX;
  Text(d,ramDetail?"Available RAM":data.ElementAtOrDefault(selected)?.Name??"All tools",x,64,12,muted);Text(d,ramDetail?$"{memory.Available/1073741824d:0.0} GB":Value(selected),x,87,34,white);if(ramDetail){MemoryBar(d,new Rect(x,174,200,5));Text(d,$"{memory.FreeRatio:P0} available",x+200,212,11,mint,false,false,true);Consumers(d,x,210);}else Graph(d,VerticalGraphBounds,selected);
  if(!ramDetail){Text(d,MeasureStatus(),x,220,9,muted);var m=UsageFor(selected);Text(d,"Input",x,239,12,muted);Text(d,Compact(m.Input+m.Cache),x+195,239,13,white,false,false,true);Text(d,"Output",x,264,12,muted);Text(d,Compact(m.Output),x+195,264,13,white,false,false,true);}
  if(ramDetail){Text(d,$"RAM  {memory.Available/1073741824d:0.0} GB available",x,268,11,mint);MemoryBar(d,new Rect(x,287,200,3));}d.Pop();d.Pop();
 }
 string MeasureStatus(){var meters=selected>=0?data.Where((_,i)=>i==selected):data.Where((_,i)=>order.Contains(i));var last=meters.Select(m=>m.Updated).Max();return last is DateTime date?$"Usage received at {date:HH:mm:ss}":"Waiting for usage data";}
 void PreviewCover(DrawingContext d,Rect rect){d.PushClip(new RectangleGeometry(rect,7,7));d.DrawRectangle(selection,null,rect);d.DrawImage(playingCover??ModuleIcons.Get("music"),rect);d.Pop();}
 void Caption(DrawingContext d,string text,double x,double y,double width,double size,Brush brush){var value=Format(text,size,brush,false);value.MaxTextWidth=width;value.MaxLineCount=1;value.Trimming=TextTrimming.CharacterEllipsis;d.DrawText(value,new Point(x,y));}
 void DrawProvider(DrawingContext d,int provider,Rect rect){if(provider<3)d.DrawImage(logos[provider==0?1:provider==1?0:2],rect);else{d.DrawRoundedRectangle(selection,null,rect,6,6);Text(d,(data.ElementAtOrDefault(provider)?.Name??"?")[..1].ToUpperInvariant(),rect.X+rect.Width/2,rect.Y+2,12,white,true);}}
 internal Rect ModuleRect(string module){if(preferences.LayoutOrder!=null)return ItemBounds(module);bool first=module==preferences.ModuleOrder[0];if(Vertical){double center=ModuleCount==1?242:first?226:270;return new Rect(VerticalRailX-16,center-16,32,32);}double x=(660-CurrentLength)/2;double cx=(first?504:550)*(1-Open)+(x+(first?400:446))*Open;double y=((edge=="bottom"?Height-ClosedDepth:0)+Extra+24)*(1-Open)+(edge=="bottom"?Height-PanelDepth+26:26)*Open;return new Rect(cx-16,y-16,32,32);}
 public string? ActionAt(Point point){var id=HoverKey(point);return id is "music" or "clipboard" or "camera"&&preferences.IsShown(id)?id:null;}
 void Modules(DrawingContext d){foreach(var (id,enabled) in new[]{("music",preferences.Music),("clipboard",preferences.ClipboardHistory)})if(enabled){var r=ModuleRect(id);d.DrawImage(ModuleIcons.Get(id),new Rect(r.X+5,r.Y+5,22,22));}}

 internal Rect ItemBounds(string id){
  var items=preferences.Layout;int index=Array.IndexOf(items,id);if(index<0)return Rect.Empty;
  double size=id=="cat"?30:26,cx,cy;
  if(Vertical){cx=VerticalRailX;double first=preferences.CounterAbove?94:82;cy=first+index*(304-first)/Math.Max(1,items.Length-1);if(items.Length>6)size=id=="cat"?24:18;}
  else{double x=(660-CurrentLength)/2;cx=(86+index*516d/Math.Max(1,items.Length-1))*(1-Open)+(x+74+index*416d/Math.Max(1,items.Length-1))*Open;cy=((edge=="bottom"?Height-ClosedDepth:0)+Extra+24)*(1-Open)+((edge=="bottom"?Height-PanelDepth:0)+26)*Open;}
  return new Rect(cx-size/2,cy-size/2,size,size);
 }
 void DrawCustomRail(DrawingContext d){
  foreach(var id in preferences.Layout){var r=ItemBounds(id);if(id=="cat"){Cat(d,r);continue;}if(id is "music" or "clipboard" or "camera"){
    if(id=="music"&&HasMusicPreview&&Open<.5)PreviewCover(d,r);else d.DrawImage(ModuleIcons.Get(id),r);
    if(id=="music"&&HasMusicPreview&&preferences.MusicTitle&&Open<.5){double width=Vertical?46:Math.Min(80,480d/Math.Max(1,preferences.Layout.Length));Caption(d,playingTitle,r.X+r.Width/2-width/2,r.Bottom+2,width,8,muted);}continue;}
   int provider=Array.IndexOf(preferences.Ids,id);DrawProvider(d,provider,r);if(Vertical&&preferences.Layout.Length<=6)Text(d,Value(provider),r.X+r.Width/2,r.Bottom+3,8,white,true);else if(!Vertical&&Open<.35)Text(d,Value(provider),r.X+r.Width/2,r.Bottom+4,9,white,true);
  }
  if(preferences.CounterAbove){double x=Vertical?VerticalRailX:330,y=Vertical?63:(edge=="bottom"?Height-ClosedDepth:0)+1;Text(d,Compact(UsageFor(Pinned).Total),x,y,9,mint,true);}
 }
 static readonly Lazy<ImageSource> editorCat=new(()=>LoadCats()[0]);
 internal static ImageSource CatPreview()=>editorCat.Value;
 void Graph(DrawingContext d,Rect r,int provider){
  var a=SamplesFor(provider);if(a.Length==0){d.DrawLine(new Pen(mint,1.3),r.BottomLeft,r.BottomRight);return;}
  double max=Math.Max(1,a.Max());var points=a.Select((v,i)=>new Point(a.Length==1?r.Right:r.Left+i*r.Width/(a.Length-1),r.Bottom-v/max*r.Height)).ToArray();
  var curve=new StreamGeometry();using(var c=curve.Open()){c.BeginFigure(points[0],false,false);for(int i=1;i<points.Length;i++){var p=points[i-1];var q=points[i];double mid=(p.X+q.X)/2;c.BezierTo(new Point(mid,p.Y),new Point(mid,q.Y),q,true,false);}}
  var fill=new StreamGeometry();using(var c=fill.Open()){c.BeginFigure(r.BottomLeft,true,true);c.LineTo(points[0],true,false);for(int i=1;i<points.Length;i++){var p=points[i-1];var q=points[i];double mid=(p.X+q.X)/2;c.BezierTo(new Point(mid,p.Y),new Point(mid,q.Y),q,true,false);}c.LineTo(r.BottomRight,true,false);}
  d.DrawGeometry(new LinearGradientBrush(Color.FromArgb(44,142,221,182),Color.FromArgb(0,142,221,182),90),null,fill);d.DrawGeometry(null,new Pen(mint,1.3),curve);d.DrawEllipse(mint,null,points[^1],3.3,3.3);
  int hit=pointer is Point cursor?SampleIndex(r,cursor,a.Length):-1;
  if(hit>=0&&Open>.95){var dot=points[hit];d.DrawLine(new Pen(muted,1){DashStyle=DashStyles.Dot},new Point(dot.X,r.Top),new Point(dot.X,r.Bottom));d.DrawEllipse(Brushes.Black,new Pen(mint,1.5),dot,4,4);var time=sampleTimes.ElementAtOrDefault(hit);Bubble(d,$"{time:HH:mm:ss}  ·  +{a[hit]:N0} tokens",r.Left,r.Top-28,r.Width);}
 }
 void Consumers(DrawingContext d,double x,double y){
  for(int i=0;i<consumers.Length;i++){var app=consumers[i];var r=new Rect(x+i*27,y,20,20);bool hover=pointer is Point p&&r.Contains(p);if(hover)d.DrawRoundedRectangle(selection,null,new Rect(r.X-3,r.Y-3,26,26),7,7);
   if(app.Icon!=null)d.DrawImage(app.Icon,r);else{d.DrawRoundedRectangle(line,null,r,5,5);Text(d,app.Name[..1].ToUpperInvariant(),r.X+10,r.Y+2,11,white,true);}
   if(hover)Bubble(d,$"{app.Name} · {app.Bytes/1073741824d:0.0} GB · {app.Count} proc.",Vertical?x:x-75,y-32,Vertical?200:270);
  }
 }
 void Bubble(DrawingContext d,string value,double x,double y,double width){
  var text=Format(value,10.5,white,false);text.MaxTextWidth=width-18;text.Trimming=TextTrimming.CharacterEllipsis;
  d.DrawRoundedRectangle(selection,new Pen(line,1),new Rect(x,y,width,25),7,7);d.DrawText(text,new Point(x+9,y+5));
 }
 bool Connected(int p)=>data.ElementAtOrDefault(p)?.Status.StartsWith("Connected")==true;
 string Value(int p)=>p<0?(data.Length==0?"—":Compact(UsageFor(-1).Total)):data.ElementAtOrDefault(p) is { } m?(m.Status.StartsWith("Source not found")||m.Status.StartsWith("Error")?"—":Compact(m.Value.Total)):"—";
 void MemoryBar(DrawingContext d,Rect r){d.DrawRoundedRectangle(line,null,r,r.Height/2,r.Height/2);d.DrawRoundedRectangle(mint,null,new Rect(r.X,r.Y,r.Width*memory.FreeRatio,r.Height),r.Height/2,r.Height/2);}
 internal static double CatWarmth(double free)=>Math.Clamp((.40-free)/.20,0,1);
 internal static double CatHeat(double free)=>Math.Clamp((.20-free)/.15,0,1);
 static ImageSource TintCat(ImageSource source,Color tint){
  var converted=new FormatConvertedBitmap((BitmapSource)source,PixelFormats.Bgra32,null,0);int stride=converted.PixelWidth*4;byte[] pixels=new byte[stride*converted.PixelHeight];converted.CopyPixels(pixels,stride,0);
  for(int i=0;i<pixels.Length;i+=4){pixels[i]=(byte)(pixels[i]*.35+tint.B*.65);pixels[i+1]=(byte)(pixels[i+1]*.35+tint.G*.65);pixels[i+2]=(byte)(pixels[i+2]*.35+tint.R*.65);}
  var image=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride);image.Freeze();return image;
 }
 internal void AdvanceCat(){catFrame=(catFrame+1)%cats.Length;}
 internal void Cat(DrawingContext d,Rect r){
  double red=preferences.CatRed/100,orange=preferences.CatOrange/100;double heat=Math.Clamp((red-memory.FreeRatio)/Math.Max(.01,red*.75),0,1);int frame=SystemParameters.ClientAreaAnimation?catFrame:0;
  if(SystemParameters.ClientAreaAnimation&&memory.FreeRatio<red+.05)r.Offset(0,-Math.Abs(Math.Sin(frame*Math.PI/4))*(1+heat));
  d.DrawImage(cats[frame],r);double warmth=Math.Clamp((orange-memory.FreeRatio)/(orange-red),0,1);
  if(warmth>0){d.PushOpacity(warmth);d.DrawImage(warmCats[frame],r);d.Pop();}
  if(heat>0){d.PushOpacity(heat);d.DrawImage(hotCats[frame],r);d.Pop();}
 }
 double Measure(string s,double size)=>Format(s,size,white,false).Width;
 FormattedText Format(string s,double size,Brush brush,bool strong)=>new(s,CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,strong?bold:normal,size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);
 void Text(DrawingContext d,string s,double x,double y,double size,Brush brush,bool center=false,bool strong=false,bool right=false){var f=Format(s,size,brush,strong);if(center)x-=f.Width/2;else if(right)x-=f.Width;d.DrawText(f,new Point(x,y));}
 internal static string Compact(double n)=>n>=1e9?(n/1e9).ToString("0.0",CultureInfo.GetCultureInfo("en-US"))+"G":n>=1e6?(n/1e6).ToString("0.0",CultureInfo.GetCultureInfo("en-US"))+"m":n>=1000?(n/1000).ToString("0.0",CultureInfo.GetCultureInfo("en-US"))+"k":n.ToString("0");
 internal static StreamGeometry Shape(double length,double depth,double floating=0){
  var g=new StreamGeometry();double f=Math.Clamp(floating,0,1),shoulder=Math.Min(36,depth*.44),radius=Math.Min(36,depth*.48),inset=56-20*Math.Clamp((depth-48)/128,0,1),rightInset=Math.Min(inset,30),r=Math.Min(24,depth/2),k=.5522847498;
  Point Mix(double ax,double ay,double bx,double by)=>new(ax+(bx-ax)*f,ay+(by-ay)*f);
  using(var c=g.Open()){
   c.BeginFigure(Mix(0,0,r,0),true,true);c.LineTo(Mix(length,0,length-r,0),true,false);
   c.BezierTo(Mix(length-18,0,length-r+k*r,0),Mix(length-rightInset,0,length,r-k*r),Mix(length-rightInset,shoulder,length,r),true,false);
   c.LineTo(Mix(length-rightInset,depth-radius,length,depth-r),true,false);
   c.BezierTo(Mix(length-rightInset,depth-radius*.45,length,depth-r+k*r),Mix(length-rightInset-radius*.45,depth,length-r+k*r,depth),Mix(length-rightInset-radius,depth,length-r,depth),true,false);
   c.LineTo(Mix(inset+radius,depth,r,depth),true,false);
   c.BezierTo(Mix(inset+radius*.45,depth,r-k*r,depth),Mix(inset,depth-radius*.45,0,depth-r+k*r),Mix(inset,depth-radius,0,depth-r),true,false);
   c.LineTo(Mix(inset,shoulder,0,r),true,false);
   c.BezierTo(Mix(inset,0,0,r-k*r),Mix(18,0,r-k*r,0),Mix(0,0,r,0),true,false);
  }return g;
 }
 internal static ImageSource Logo(string name){var xml=XDocument.Load(Path.Combine(AppContext.BaseDirectory,"assets",name+".svg"));var box=xml.Root!.Attribute("viewBox")!.Value.Split(' ').Select(s=>double.Parse(s,CultureInfo.InvariantCulture)).ToArray();var group=new DrawingGroup();foreach(var p in xml.Descendants().Where(n=>n.Name.LocalName=="path")){var color=p.Attribute("fill")?.Value??"white";if(color=="currentColor")color="white";group.Children.Add(new GeometryDrawing((Brush)new BrushConverter().ConvertFromString(color)!,null,Geometry.Parse(p.Attribute("d")!.Value)));}group.Transform=new ScaleTransform(24/box[2],24/box[3]);group.Freeze();var image=new DrawingImage(group);image.Freeze();return image;}
 static ImageSource[] LoadCats(){var source=new BitmapImage();source.BeginInit();source.UriSource=new Uri(Path.Combine(AppContext.BaseDirectory,"assets","cat-run.png"));source.CacheOption=BitmapCacheOption.OnLoad;source.EndInit();source.Freeze();return Enumerable.Range(0,8).Select(i=>{int a=(int)Math.Round(i*source.PixelWidth/8d),b=(int)Math.Round((i+1)*source.PixelWidth/8d);var crop=new CroppedBitmap(source,new Int32Rect(a,270,b-a,200));crop.Freeze();return (ImageSource)crop;}).ToArray();}
 protected override HitTestResult? HitTestCore(PointHitTestParameters p)=>CurrentShape().FillContains(p.HitPoint)?new PointHitTestResult(this,p.HitPoint):null;
 internal bool Contains(Point p)=>CurrentShape().FillContains(p);
 internal int Selected=>selected;
 internal void SetFixture(){data=[new Meter("Codex"){Value=new Usage(31200,6400,600),Status="Connected"},new Meter("Claude Code"){Value=new Usage(10000,2400),Status="Connected"},new Meter("OpenCode"){Value=new Usage(7100,1000),Status="Connected"}];memory=new MemoryInfo((ulong)(32*1073741824d),(ulong)(8.2*1073741824d));foreach(var q in samples){q.Clear();foreach(var n in new[]{0d,1,4,9,5,4,7,10,8,6,12,21,11,9})q.Enqueue(n);}sampleTimes.Clear();for(int i=0;i<14;i++)sampleTimes.Enqueue(DateTime.Today.AddHours(14).AddMinutes(32).AddSeconds(i));selected=-1;pointer=null;ramDetail=false;}
 internal void RenderFile(string path,bool expand,string side,double progress=-1){SetEdge(side);BeginAnimation(OpenProperty,null);Open=progress>=0?progress:expand?1:0;Measure(new Size(Width,Height));Arrange(new Rect(0,0,Width,Height));UpdateLayout();var bitmap=new RenderTargetBitmap((int)Width*2,(int)Height*2,192,192,PixelFormats.Pbgra32);bitmap.Render(this);var bounds=CurrentShape().Bounds;var visual=new DrawingVisual();int w=(int)Math.Ceiling(bounds.Width*2),h=(int)Math.Ceiling(bounds.Height*2);using(var c=visual.RenderOpen()){c.DrawRectangle(Solid(121,119,188),null,new Rect(0,0,w/2d,h/2d));c.DrawImage(bitmap,new Rect(-bounds.X,-bounds.Y,Width,Height));}var composed=new RenderTargetBitmap(w,h,192,192,PixelFormats.Pbgra32);composed.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(composed));using var f=File.Create(path);encoder.Save(f);}
}
