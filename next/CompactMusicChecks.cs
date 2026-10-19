using System.Windows;
using System.Windows.Threading;
namespace TokenDock;

static class CompactMusicChecks {
 public static void Run(Action<bool> check,string dir){
  using var music=new MusicModule(Dispatcher.CurrentDispatcher);var surface=new NotchSurface{Running=false};surface.SetFixture();var prefs=new Preferences{Music=true,ClipboardHistory=true,CounterAbove=true};surface.Configure(prefs);
  Action refresh=()=>surface.SetNowPlaying(music);music.Changed+=refresh;
  var cover=MusicModule.DecodeArtwork(File.ReadAllBytes(Path.Combine(dir,"fixture-album.png")));var timeline=new MusicTimeline(TimeSpan.Zero,TimeSpan.FromMinutes(4),TimeSpan.FromSeconds(72),DateTimeOffset.UtcNow,1,true,TimeSpan.Zero,TimeSpan.FromMinutes(4));music.SetFixture(cover,timeline);check(surface.HasMusicPreview);
  foreach(string edge in new[]{"top","bottom","left","right"}){
   surface.SetEdge(edge);surface.Open=0;surface.ClearHover();var area=surface.MusicPreviewBounds;var target=new Point(area.X+area.Width/2,area.Y+area.Height/2);
   check(new[]{area.TopLeft,area.TopRight,area.BottomLeft,area.BottomRight}.All(surface.Contains));check(surface.WantsMusicPreview(target));surface.SelectHover(target);check(surface.Selected==-1);
   surface.RenderFile(Path.Combine(dir,$"now-playing-{edge}.png"),false,edge);
   int created=0;System.Windows.FrameworkElement View(){created++;return new InlineModuleView(music,null);}
   check(surface.TryOpenMusicPreview(target,View));check(surface.ActiveModule=="music"&&created==1);check(surface.TryOpenMusicPreview(target,View)&&created==1);
   foreach(double open in new[]{.25,.6,1}){surface.Open=open;surface.SelectHover(target);check(surface.ActiveModule=="music");}
   surface.RenderFile(Path.Combine(dir,$"now-playing-{edge}-reader.png"),true,edge);if(edge is "left" or "right"){var rail=surface.VerticalRailContentBounds().ToArray();check(rail[1].Bottom<=rail[2].Top&&rail[3].Bottom<=rail[4].Top);}
   var body=surface.ModuleBounds;surface.SelectHover(new Point(body.X+20,body.Y+20));check(surface.ActiveModule=="music");
   var logo=edge is "left" or "right"?surface.VerticalRailIconBounds(0):new Rect(113,15,22,22);surface.SelectHover(new Point(logo.X+11,logo.Y+11));check(surface.ActiveModule==null&&surface.Selected==1);
   surface.Open=0;var module=surface.ModuleRect("music");check(!surface.TryOpenMusicPreview(new Point(module.X+16,module.Y+16),View));check(created==1);
  }
  music.SetFixture(cover,timeline with{Playing=false});check(!surface.HasMusicPreview);surface.RenderFile(Path.Combine(dir,"now-playing-paused-rest.png"),false,"top");check(!surface.WantsMusicPreview(new Point(200,40)));
  music.SetFixture(null,timeline);check(surface.HasMusicPreview);surface.RenderFile(Path.Combine(dir,"now-playing-no-cover.png"),false,"top");
  prefs.Music=false;surface.Configure(prefs);check(!surface.HasMusicPreview);prefs.Music=true;prefs.CounterAbove=false;surface.Configure(prefs);surface.RenderFile(Path.Combine(dir,"now-playing-compact.png"),false,"top");check(surface.Contains(surface.MusicPreviewBounds.BottomRight));
  surface.SetFloating(true,false);surface.RenderFile(Path.Combine(dir,"now-playing-floating.png"),false,"right");music.Changed-=refresh;surface.CloseModule();surface.Running=false;
 }
}
