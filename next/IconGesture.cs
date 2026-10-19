using System.Windows;
using System.Windows.Media;
namespace TokenDock;

sealed record IconDrop(string Source,string? Target,bool After,bool Moved);
sealed class IconGesture {
 public readonly string Source;readonly Point start;readonly bool vertical;readonly Dictionary<string,Rect> slots;
 public bool Moved {get;private set;}public string? Target {get;private set;}public bool After {get;private set;}public Point Pointer {get;private set;}
 public IconGesture(string source,Point start,bool vertical,Dictionary<string,Rect> slots){Source=source;this.start=start;this.vertical=vertical;this.slots=slots;Pointer=start;}
 public Rect TargetBounds=>Target!=null?slots[Target]:Rect.Empty;
 public void Update(Point point){Pointer=point;if((point-start).Length>=6)Moved=true;Target=null;if(!Moved)return;var area=Rect.Empty;foreach(var r in slots.Values)area.Union(r);area.Inflate(12,12);if(!area.Contains(point))return;
  var candidates=slots.Where(k=>k.Key!=Source).Select(k=>(k.Key,k.Value,Distance:vertical?Math.Abs(point.Y-(k.Value.Y+k.Value.Height/2)):Math.Abs(point.X-(k.Value.X+k.Value.Width/2)))).OrderBy(k=>k.Distance);
  foreach(var candidate in candidates){var r=candidate.Value;double across=vertical?Math.Abs(point.X-(r.X+r.Width/2)):Math.Abs(point.Y-(r.Y+r.Height/2));if(across>30||candidate.Distance>90)continue;Target=candidate.Key;After=vertical?point.Y>r.Y+r.Height/2:point.X>r.X+r.Width/2;break;}
 }
 public IconDrop Drop(Point point){Update(point);return new(Source,Target,After,Moved);}
}

sealed partial class NotchSurface {
 IconGesture? iconGesture;internal bool IsIconDrag=>iconGesture!=null;
 internal Rect VisibleItemBounds(string id){
  if(preferences.LayoutOrder!=null)return ItemBounds(id);
  if(id=="music"&&HasMusicPreview&&Open<.5)return MusicPreviewBounds;
  if(id is "music" or "clipboard" or "camera")return ModuleRect(id);
  double x=(660-CurrentLength)/2,y=edge=="bottom"?Height-PanelDepth:0,restY=(edge=="bottom"?Height-ClosedDepth:0)+Extra;
  if(id=="cat")return Vertical?new Rect(VerticalRailX-17,CatTop,34,32):new Rect(588*(1-Open)+(x+472)*Open,(restY+10)*(1-Open)+(y+8)*Open,30+5*Open,29+5*Open);
  int provider=Array.IndexOf(preferences.Ids,id),at=Array.IndexOf(order,provider);if(at<0||HasMusicPreview&&Open<.5)return Rect.Empty;
  return Vertical?VerticalRailIconBounds(at):new Rect((85+at*Slot)*(1-Open)+(x+74+at*49)*Open-11,(restY+24)*(1-Open)+(y+26)*Open-11,22,22);
 }
 internal bool BeginIconDrag(Point point){
  var slots=preferences.Layout.Select(id=>(id,rect:VisibleItemBounds(id))).Where(v=>!v.rect.IsEmpty).ToDictionary(v=>v.id,v=>v.rect);
  string? source=slots.FirstOrDefault(k=>k.Value.Contains(point)).Key;if(source==null)return false;
  double open=Open;BeginAnimation(OpenProperty,null);Open=open;openMotion=null;double body=BodyHeight;BeginAnimation(BodyHeightProperty,null);BodyHeight=body;sizeMotion=null;iconGesture=new IconGesture(source,point,Vertical,slots);return true;
 }
 internal void UpdateIconDrag(Point point){iconGesture?.Update(point);InvalidateVisual();}
 internal IconDrop? EndIconDrag(Point point){var drop=iconGesture?.Drop(point);if(drop!=null&&!Contains(point))drop=drop with{Target=null};CancelIconDrag();return drop;}
 internal void CancelIconDrag(){if(iconGesture==null)return;iconGesture=null;ResizeModule();if(running)Animate(OpenProperty,expanded?1:0,ref openMotion,ref openStart,Vertical?15:17);InvalidateVisual();}
 void DrawIconDrag(DrawingContext d){
  if(iconGesture is not{Moved:true} gesture)return;
  if(!gesture.TargetBounds.IsEmpty){var r=gesture.TargetBounds;var pen=new Pen(mint,2);if(Vertical){double y=gesture.After?r.Bottom+5:r.Top-5;d.DrawLine(pen,new Point(r.X-2,y),new Point(r.Right+2,y));}else{double x=gesture.After?r.Right+5:r.Left-5;d.DrawLine(pen,new Point(x,r.Y-2),new Point(x,r.Bottom+2));}}
  var ghost=new Rect(gesture.Pointer.X-13,gesture.Pointer.Y-13,26,26);d.PushOpacity(.75);
  if(gesture.Source=="cat")Cat(d,ghost);else if(gesture.Source is "music" or "clipboard" or "camera")d.DrawImage(ModuleIcons.Get(gesture.Source),ghost);else DrawProvider(d,Array.IndexOf(preferences.Ids,gesture.Source),ghost);d.Pop();
 }
}
