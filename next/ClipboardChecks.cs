using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace TokenDock;

// Fixtures and injected writes only: never reads or changes the system clipboard.
static class ClipboardChecks {
 static IEnumerable<T> Find<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Find<T>(child))yield return nested;}}
 public static void Run(Action<bool> check,string dir){
  var image=new BitmapImage(new Uri(Path.Combine(dir,"fixture-album.png")));var picture=CopyItem.FromImage(image)!;
  var square=CameraPane.SquarePhoto(image);check(square.PixelWidth==square.PixelHeight&&square.IsFrozen);
  var round=CameraPane.RoundPhoto(image);check(round.PixelWidth==Math.Min(image.PixelWidth,image.PixelHeight)&&round.PixelWidth==round.PixelHeight);byte[] roundPixels=new byte[round.PixelWidth*round.PixelHeight*4];round.CopyPixels(roundPixels,round.PixelWidth*4,0);check(roundPixels[3]==0&&roundPixels[(round.PixelWidth-1)*4+3]==0);check(roundPixels[((round.PixelHeight/2)*round.PixelWidth+round.PixelWidth/2)*4+3]>0);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(round));using(var output=File.Create(Path.Combine(dir,"camera-round-photo.png")))png.Save(output);var roundCopy=CopyItem.FromPng(File.ReadAllBytes(Path.Combine(dir,"camera-round-photo.png")))!;check(roundCopy.Image!.PixelWidth==round.PixelWidth);
  check(picture.Kind==CopyKind.Image&&picture.Image!.IsFrozen);check(picture.Bytes==image.PixelWidth*image.PixelHeight*4L);
  check(CopyItem.FromImage(image)!.Key==picture.Key);check(CopyItem.FromPng(File.ReadAllBytes(Path.Combine(dir,"fixture-album.png")))!.Key==picture.Key);
  check(CopyItem.FromPng([1,2,3])==null);check(!CopyItem.ValidImageSize(int.MaxValue,int.MaxValue));check(!CopyItem.ValidImageSize(0,20));check(!CopyItem.ValidImageSize(4096,4096));
  byte[] pngHeader=new byte[24];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(pngHeader,0);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(pngHeader.AsSpan(16,4),int.MaxValue);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(pngHeader.AsSpan(20,4),int.MaxValue);check(CopyItem.FromPng(pngHeader)==null);
  string[] paths=[@"C:\Fixture\Maquette.png",@"C:\Fixture\Notes.pdf"];var files=CopyItem.FromFiles(paths)!;paths[0]=@"C:\Different.png";check(files.Files[0]==@"C:\Fixture\Maquette.png");check(files.Label=="2 files"&&files.Preview=="Maquette.png +1");
  check(CopyItem.FromFiles([])==null);check(CopyItem.FromFiles(["relative.txt"])==null);check(CopyItem.FromFiles(["C:\\bad\0name"])==null);check(CopyItem.FromFiles(Enumerable.Repeat(@"C:\Fixture.txt",101).ToArray())==null);
  var history=new CopyHistory();history.Add(picture);history.Add(CopyItem.FromImage(image));check(history.Items.Count==1);
  foreach(int n in Enumerable.Range(0,3))history.Add(picture with{Key=$"budget-{n}",Bytes=CopyItem.MaxImageBytes});check(history.Bytes<=32*1024*1024&&history.Items.Count==2&&history.Items[0].Key=="budget-2");history.Clear();check(history.Bytes==0);
  using var clipboard=new ClipboardModule();clipboard.History.Add("Meeting tomorrow at 10 AM");clipboard.History.Add(files);clipboard.History.Add(picture);
  var surface=new NotchSurface{Running=false};surface.SetFixture();surface.Configure(new Preferences{Music=true,ClipboardHistory=true});
  var writes=new List<CopyKind>();
  foreach(string edge in new[]{"top","bottom","left","right"}){
   surface.SetEdge(edge);surface.ShowModule("clipboard",new InlineModuleView(null,clipboard,item=>writes.Add(item.Kind)));surface.RenderFile(Path.Combine(dir,$"clipboard-rich-{edge}.png"),true,edge);
   check(Find<InlineModuleView>(surface).Single().AllowDrop);check(Find<ScrollViewer>(surface).Single().AllowDrop);
   check(!Find<TextBlock>(surface).Any(t=>t.Text.Contains("session")||t.Text=="←"));check(Find<Image>(surface).Any(i=>ReferenceEquals(i.Source,picture.Image)));
   foreach(var kind in new[]{"Image","2 files","Text"}){
    var button=Find<Button>(surface).Single(b=>AutomationProperties.GetName(b)=="Copy: "+kind);button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));surface.UpdateLayout();check(Find<TextBlock>(surface).Count(t=>t.Text=="✓")==1);check(Find<Button>(surface).Count(b=>AutomationProperties.GetName(b)=="Copied")==1);
   }
   check(writes.TakeLast(3).SequenceEqual(new[]{CopyKind.Image,CopyKind.Files,CopyKind.Text}));Find<Button>(surface).Single(b=>AutomationProperties.GetName(b)=="Copy: Image").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));surface.UpdateLayout();surface.RenderFile(Path.Combine(dir,$"clipboard-copied-{edge}.png"),true,edge);
  }
  using(var photos=new ClipboardModule()){int updates=0;photos.Changed+=()=>updates++;photos.AddPhoto(Path.Combine(dir,"fixture-album.png"),image);var captured=photos.History.Items.Single();check(captured.Kind==CopyKind.Image&&captured.Image!=null);check(captured.Files.Length==1&&File.Exists(captured.Files[0]));check(updates==1);photos.Remove(captured);check(photos.History.Items.Count==0&&File.Exists(Path.Combine(dir,"fixture-album.png")));}
  surface.SetEdge("right");surface.ShowModule("clipboard",new InlineModuleView(null,clipboard,_=>throw new ExternalException("Fixture: busy")));surface.RenderFile(Path.Combine(dir,"clipboard-before-retry.png"),true,"right");Find<Button>(surface).First(b=>AutomationProperties.GetName(b).StartsWith("Copy:")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));surface.UpdateLayout();check(Find<TextBlock>(surface).Any(t=>t.Text=="Retry"));check(!Find<TextBlock>(surface).Any(t=>t.Text=="✓"));
  int before=clipboard.History.Items.Count,written=writes.Count;
  Find<Button>(surface).Single(b=>AutomationProperties.GetName(b)=="Remove: 2 files").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));surface.UpdateLayout();
  check(clipboard.History.Items.Count==before-1);check(!clipboard.History.Items.Any(i=>i.Kind==CopyKind.Files));check(clipboard.History.Items.Any(i=>i.Kind==CopyKind.Image));check(writes.Count==written);
  Find<Button>(surface).Single(b=>b.Content is string text&&text=="Clear").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(clipboard.History.Items.Count==0);check(!Find<TextBlock>(surface).Any(t=>t.Text=="Retry"));surface.CloseModule();
 }
}
