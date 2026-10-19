using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
namespace TokenDock;
sealed class SettingsPane:StackPanel {
 readonly Preferences draft;readonly StackPanel preview=new(){Orientation=Orientation.Horizontal,Spacing=4};
 string? moving;Point press;
 public SettingsPane(Preferences preferences,Action<Preferences> apply,Action cancel){
  draft=preferences.Copy();Spacing=14;Children.Add(Ui.Text("Customize",22));Children.Add(Ui.Text("Drag to arrange your dock",12,Ui.Muted));Children.Add(new ScrollViewer{Content=preview,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled});BuildPreview();
  var options=new StackPanel{Spacing=10};
  foreach(var id in draft.Ids.Concat(new[]{"music","clipboard","camera","cat"})){
   string label=id switch{"music"=>"Music","clipboard"=>"Clipboard history","camera"=>"Camera","cat"=>"RAM cat",_=>draft.Name(id)};
   var toggle=new CheckBox{Content=label,IsChecked=id=="clipboard"?draft.ClipboardHistory:draft.IsShown(id)};
   toggle.IsCheckedChanged+=(_,_)=>{draft.ShowItem(id,toggle.IsChecked==true);BuildPreview();};options.Children.Add(toggle);
  }
  void Toggle(string label,bool value,Action<bool> changed){var check=new CheckBox{Content=label,IsChecked=value};check.IsCheckedChanged+=(_,_)=>changed(check.IsChecked==true);options.Children.Add(check);}
  Toggle("Album artwork",draft.MusicPreview,v=>draft.MusicPreview=v);Toggle("Track title",draft.MusicTitle,v=>draft.MusicTitle=v);Toggle("Featured token counter",draft.CounterAbove,v=>draft.CounterAbove=v);Toggle("Reduce motion",draft.ReduceMotion,v=>draft.ReduceMotion=v);
  var counter=new ComboBox{ItemsSource=new[]{"all"}.Concat(draft.Ids).ToArray(),SelectedItem=draft.Counter,HorizontalAlignment=HorizontalAlignment.Stretch};counter.SelectionChanged+=(_,_)=>draft.Counter=counter.SelectedItem as string??"all";options.Children.Add(counter);
  var catOptions=new StackPanel{Spacing=8};var cat=new Cat(draft);var memory=new Slider{Minimum=0,Maximum=100,Value=30};cat.Update(new MemoryInfo(100,30));memory.ValueChanged+=(_,_)=>cat.Update(new MemoryInfo(100,(ulong)memory.Value));
  void Slider(string title,double min,double max,double value,Action<double> changed){var label=Ui.Text($"{title} · {value:0.##}",12,Ui.Muted);var slider=new Slider{Minimum=min,Maximum=max,Value=value};Avalonia.Automation.AutomationProperties.SetName(slider,title);slider.ValueChanged+=(_,_)=>{changed(slider.Value);label.Text=$"{title} · {slider.Value:0.##}";cat.Update(new MemoryInfo(100,(ulong)memory.Value));};catOptions.Children.Add(label);catOptions.Children.Add(slider);}
  Slider("Orange below (%)",2,100,draft.CatOrange,v=>draft.CatOrange=v);Slider("Red below (%)",1,99,draft.CatRed,v=>draft.CatRed=v);Slider("Animation speed",.25,2,draft.CatSpeed,v=>draft.CatSpeed=v);catOptions.Children.Add(cat);catOptions.Children.Add(Ui.Text("Preview available RAM",12,Ui.Muted));catOptions.Children.Add(memory);options.Children.Add(new Expander{Header="RAM animation",Content=catOptions});
  var sources=new StackPanel{Spacing=8};var name=new TextBox{PlaceholderText="Tool name",MaxLength=32};sources.Children.Add(name);
  var add=Ui.Button("＋ Local JSON source",async()=>{
   var window=TopLevel.GetTopLevel(this);if(window==null||draft.Sources.Length>=3)return;
   var files=await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Select cumulative usage JSON",AllowMultiple=false,FileTypeFilter=[new FilePickerFileType("JSON"){Patterns=["*.json"]}]});
   var path=files.FirstOrDefault()?.TryGetLocalPath();if(path==null||string.IsNullOrWhiteSpace(name.Text))return;
   var source=new CustomSource("custom-"+Guid.NewGuid().ToString("N"),name.Text.Trim(),path);draft.Sources=draft.Sources.Append(source).ToArray();draft.ShowItem(source.Id,true);BuildPreview();sources.Children.Add(Ui.Text(source.Name,13,Ui.Mint));
  });sources.Children.Add(add);sources.Children.Add(Ui.Text("Local JSON: cumulative input, output and cache. Up to 3 sources.",12,Ui.Muted));options.Children.Add(new Expander{Header="Custom sources",Content=sources});
  Children.Add(new ScrollViewer{Content=options,MaxHeight=280});var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(Ui.Button("Cancel",cancel));actions.Children.Add(Ui.Button("Apply",()=>{draft.Normalize();apply(draft);}));Children.Add(actions);
 }
 void BuildPreview(){preview.Children.Clear();foreach(var id in draft.Layout){
  var button=Ui.Button(id=="cat"?new Cat(draft):Ui.Icon(id,24),()=>{},id);button.Padding=new Thickness(6);button.Width=38;button.Height=40;button.Tag=id;
  button.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(button).Properties.IsLeftButtonPressed){moving=id;press=e.GetPosition(preview);e.Pointer.Capture(button);e.Handled=true;}};
  button.PointerReleased+=(_,e)=>{e.Pointer.Capture(null);if(moving!=id)return;moving=null;var point=e.GetPosition(preview);if(((Vector)(point-press)).Length<5)return;var target=preview.Children.OfType<Button>().FirstOrDefault(b=>b.Bounds.Contains(point))?.Tag as string;if(target!=null){draft.MoveItem(id,target);BuildPreview();}e.Handled=true;};preview.Children.Add(button);
 }}
}
