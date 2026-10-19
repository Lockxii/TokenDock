using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace TokenDock;

static class InlineChecks {
 sealed class DisposableView:Border,IDisposable{public bool Disposed;public void Dispose()=>Disposed=true;}
 static IEnumerable<T> Descendants<T>(DependencyObject parent)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T found)yield return found;foreach(var match in Descendants<T>(child))yield return match;}}
 public static void Run(Action<bool> check,string dir){
  var surface=new NotchSurface{Running=false};surface.SetFixture();surface.Configure(new Preferences{Music=true,ClipboardHistory=true});
  using var music=new MusicModule(Dispatcher.CurrentDispatcher);using var clipboard=new ClipboardModule();
  for(int i=0;i<20;i++)clipboard.History.Add(i==19?"Texte de démonstration assez long pour vérifier la coupure propre dans une barre latérale.":$"Copie de démonstration {i+1:00}");
  foreach(string edge in new[]{"top","bottom","left","right"})foreach(string module in new[]{"music","clipboard"}){
   surface.SetEdge(edge);surface.ClearHover();surface.ShowModule(module,new InlineModuleView(module=="music"?music:null,module=="clipboard"?clipboard:null));
   check(surface.ActiveModule==module);surface.Open=1;var bounds=surface.ModuleBounds;check(new[]{bounds.TopLeft,bounds.TopRight,bounds.BottomLeft,bounds.BottomRight}.All(surface.Contains));check(surface.IsModuleContent(new Point(bounds.X+10,bounds.Y+10)));
   surface.RenderFile(Path.Combine(dir,$"inline-{module}-{edge}.png"),true,edge);check(Window.GetWindow(surface)==null);
   var buttons=Descendants<Button>(surface).ToArray();check(buttons.Length==(module=="music"?3:41));
   // Native button hit targets remain descendants of the same notch, rather than a popup/window.
   var target=buttons.Last();var center=target.TranslatePoint(new Point(target.ActualWidth/2,target.ActualHeight/2),surface);
   if(module=="music"){check(surface.IsModuleContent(center));check(!buttons.Any(b=>b.IsEnabled));}
   else{var scroll=Descendants<ScrollViewer>(surface).Single();check(scroll.ScrollableHeight>0);scroll.ScrollToBottom();surface.UpdateLayout();check(scroll.VerticalOffset>0);surface.RenderFile(Path.Combine(dir,$"inline-clipboard-{edge}-scrolled.png"),true,edge);}
   foreach(double progress in new[]{0d,.25,.5,.75}){surface.Open=progress;surface.Measure(new Size(surface.Width,surface.Height));surface.Arrange(new Rect(0,0,surface.Width,surface.Height));surface.UpdateLayout();check(!surface.IsModuleContent(new Point(bounds.X+10,bounds.Y+10)));check(!Descendants<Border>(surface).First().IsHitTestVisible);}
   surface.RenderFile(Path.Combine(dir,$"inline-{module}-{edge}-transition.png"),true,edge,.75);
   surface.Open=1;surface.CloseModule();check(surface.ActiveModule==null);
  }
  surface.SetEdge("right");surface.Open=1;var first=new DisposableView();surface.ShowModule("music",first);surface.ShowModule("clipboard",new InlineModuleView(null,clipboard));check(first.Disposed);
  surface.RenderFile(Path.Combine(dir,"inline-history-before-clear.png"),true,"right");var clear=Descendants<Button>(surface).Single(b=>b.Content is string s&&s=="Effacer");clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(clipboard.History.Items.Count==0);surface.RenderFile(Path.Combine(dir,"inline-history-empty.png"),true,"right");check(Descendants<Button>(surface).Count()==1);
  surface.SelectHover(new Point(294,82));check(surface.ActiveModule==null&&surface.Selected==1);
  surface.ShowModule("music",new InlineModuleView(music,null));surface.SetFloating(true,false);surface.RenderFile(Path.Combine(dir,"inline-music-floating.png"),true,"top");surface.CloseModule();check(surface.Clip==null);
  check(music.Status=="Désactivé"&&clipboard.Status=="Désactivé");surface.Running=false;
 }
}
