using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
namespace TokenDock;

// Two independent native processes under Xvfb exercise the real Xdnd boundary.
// Their only inputs are generated fixtures in a marked temporary test directory.
sealed class NativeDragWindow:Window {
 readonly Copies copies;readonly string directory,role;CopyItem? last;
 public NativeDragWindow(string role,string directory){
  if(Environment.GetEnvironmentVariable("CI")!="true"||Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated"||!File.Exists(Path.Combine(directory,"fixture.marker")))throw new InvalidOperationException("Drag checks require isolated CI fixtures.");
  this.directory=directory;this.role=role;copies=new Copies(this);
  Title="TokenDock drag "+role;WindowDecorations=WindowDecorations.None;Width=300;Height=240;Position=new PixelPoint(role=="source"?60:460,60);Background=Brushes.Black;
  var stack=new StackPanel{Spacing=12,Margin=new Thickness(20)};Content=stack;
  if(role=="source"){
   Bind(stack,"File",()=>CopyItem.FromFiles([Path.Combine(directory,"space # café.txt")])!);
   Bind(stack,"Round photo",()=>CopyItem.FromPng(File.ReadAllBytes(Path.Combine(directory,"round.png")),[Path.Combine(directory,"round.png")])!);
  }else Bind(stack,"Drag the stored snapshot back",()=>last);
  stack.Children.Add(new TextBlock{Text="Drop here",Height=80,Foreground=Brushes.White});
  DragDrop.SetAllowDrop(this,true);
  AddHandler(DragDrop.DragOverEvent,(_,e)=>{e.DragEffects=e.DataTransfer.Contains(DataFormat.File)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;});
  AddHandler(DragDrop.DropEvent,async(_,e)=>{
   try{
    var paths=e.DataTransfer.TryGetFiles()?.Select(f=>f.TryGetLocalPath()).Where(p=>p!=null).Cast<string>().ToArray()??[];
    if(paths.Length!=1)throw new IOException("Expected one native file URL.");
    string original=Path.Combine(directory,Path.GetFileName(paths[0]));
    if(!File.ReadAllBytes(paths[0]).SequenceEqual(File.ReadAllBytes(original)))throw new IOException("Native drop changed file bytes.");
    if(role=="sink"){
     await copies.Store(paths);last=copies.History.Items.FirstOrDefault()??throw new IOException("Shelf did not retain the drop.");
     if(last.Files[0]==original)throw new IOException("Shelf must retain an independent copy.");
    }else if(paths[0]==original)throw new IOException("Return drag must use the shelf snapshot.");
    File.AppendAllText(Path.Combine(directory,role+".events"),Path.GetFileName(original)+"\n");e.Handled=true;
   }catch(Exception error){File.WriteAllText(Path.Combine(directory,"failure"),error.ToString());}
  });
  KeyDown+=(_,e)=>{if(e.Key==Key.Escape&&Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)app.Shutdown();};
  Opened+=(_,_)=>File.WriteAllText(Path.Combine(directory,role+".ready"),"ready");
  Closed+=(_,_)=>copies.Dispose();
  if(Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)lifetime.Exit+=(_,_)=>copies.Dispose();
 }
 void Bind(StackPanel stack,string name,Func<CopyItem?> get){
  var button=new Border{Child=new TextBlock{Text=name,Foreground=Brushes.White},Background=Brushes.DarkSlateGray,Width=260,Height=44};stack.Children.Add(button);Point start=default;PointerPressedEventArgs? pressed=null;
  button.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(button).Properties.IsLeftButtonPressed){start=e.GetPosition(button);pressed=e;}};
  button.PointerMoved+=async(_,e)=>{if(pressed==null||((Vector)(e.GetPosition(button)-start)).Length<6)return;var trigger=pressed;pressed=null;var item=get();if(item==null)return;using var data=await item.Transfer(this);await DragDrop.DoDragDropAsync(trigger,data,DragDropEffects.Copy);};
  button.PointerReleased+=(_,_)=>pressed=null;
 }
}
