using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
namespace TokenDock;

// Native controls hosted inside the notch, sharing its clipping and animation.
sealed class InlineModuleView:Grid,IDisposable {
 readonly ClipboardModule? clipboard;readonly Action<CopyItem> write;
 readonly TextBlock heading=Line("",12);readonly MusicPane? musicPane;
 readonly StackPanel entries=new();readonly ScrollViewer scroll=new();
 public event Action? DesiredHeightChanged;
 public double DesiredBodyHeight=>34+66*Math.Clamp(clipboard?.History.Items.Count??0,1,3);
 string? copiedKey,errorKey;
 public InlineModuleView(MusicModule? music,ClipboardModule? clipboard,Action<CopyItem>? copyAction=null){
  this.clipboard=clipboard;write=copyAction??(item=>clipboard!.Copy(item));Resources=PanelUi.Theme();ClipToBounds=true;
  if(music!=null){musicPane=new MusicPane(music);Children.Add(musicPane);return;}
  if(clipboard==null)return;
  AllowDrop=true;Background=Brushes.Transparent;scroll.AllowDrop=true;entries.AllowDrop=true;
  RowDefinitions.Add(new RowDefinition{Height=new GridLength(34)});RowDefinitions.Add(new RowDefinition());
  var header=new DockPanel{Margin=new Thickness(0,0,0,8)};
  var clear=Button("Clear",()=>{copiedKey=errorKey=null;clipboard.Clear();},"Clear history");clear.FontSize=10;clear.Foreground=PanelUi.Muted;clear.Background=Brushes.Transparent;DockPanel.SetDock(clear,Dock.Right);header.Children.Add(clear);
  heading.FontWeight=FontWeights.SemiBold;header.Children.Add(heading);Children.Add(header);
  scroll.Content=entries;scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;SetRow(scroll,1);Children.Add(scroll);
  clipboard.Changed+=RefreshClipboard;RefreshClipboard();
 }
 static TextBlock Line(string text,double size=14,Brush? brush=null)=>new(){Text=text,FontSize=size,Foreground=brush??PanelUi.Text,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,VerticalAlignment=VerticalAlignment.Center};
 static Button Button(string text,Action action,string accessible){var button=PanelUi.Button(text,action);button.MinHeight=24;button.Padding=new Thickness(7,2,7,2);button.Margin=new Thickness(0);AutomationProperties.SetName(button,accessible);return button;}
 public void SetActive(bool active)=>musicPane?.SetActive(active);
 void RefreshClipboard(){
  if(clipboard==null)return;
  AllowDrop=true;Background=Brushes.Transparent;scroll.AllowDrop=true;entries.AllowDrop=true;double offset=scroll.VerticalOffset;heading.Text=$"Clipboard · {clipboard.History.Items.Count}";entries.Children.Clear();DesiredHeightChanged?.Invoke();
  if(clipboard.History.Items.Count==0){copiedKey=errorKey=null;var empty=new StackPanel{Margin=new Thickness(12,4,12,4)};empty.Children.Add(ModuleIcons.Image("clipboard",24));var label=Line("Drop your files here",12);label.HorizontalAlignment=HorizontalAlignment.Center;label.Margin=new Thickness(0,8,0,0);empty.Children.Add(label);entries.Children.Add(empty);return;}
  foreach(var item in clipboard.History.Items){
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(38)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(42)});
   FrameworkElement thumb=item.Image!=null?new Image{Source=item.Image,Stretch=Stretch.Uniform}:Line(item.Kind==CopyKind.Files?"\uE8A5":"\uE8D2",16,PanelUi.Muted);
   if(thumb is TextBlock glyph){glyph.FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");glyph.HorizontalAlignment=HorizontalAlignment.Center;}
   var tile=new Border{Width=30,Height=32,CornerRadius=new CornerRadius(5),Background=new SolidColorBrush(Color.FromRgb(35,36,41)),Child=thumb,HorizontalAlignment=HorizontalAlignment.Left,Clip=new RectangleGeometry(new Rect(0,0,30,32),5,5)};row.Children.Add(tile);
   var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)};labels.Children.Add(Line(item.Label,9,PanelUi.Muted));labels.Children.Add(Line(item.Preview,12));SetColumn(labels,1);row.Children.Add(labels);
   string feedback=errorKey==item.Key?"Retry":copiedKey==item.Key?"✓":"Copy";
   var copyLabel=Line(feedback,feedback=="✓"?18:9,PanelUi.Mint);copyLabel.HorizontalAlignment=HorizontalAlignment.Center;SetColumn(copyLabel,2);row.Children.Add(copyLabel);
   var button=Button("",()=>Copy(item),copiedKey==item.Key?"Copied":"Copy: "+item.Label);button.AllowDrop=true;button.Content=row;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Height=58;button.Padding=new Thickness(9,6,7,6);button.Background=new SolidColorBrush(Color.FromRgb(22,23,27));button.Margin=new Thickness(0,0,3,8);AutomationProperties.SetHelpText(button,errorKey==item.Key?"Clipboard busy. Try again.":item.Preview[..Math.Min(180,item.Preview.Length)]);if(item.Files.Length>0){Point start=default;bool armed=false,dragged=false;
    button.PreviewMouseLeftButtonDown+=(_,e)=>{start=e.GetPosition(button);armed=true;dragged=false;};
    button.PreviewMouseMove+=(_,e)=>{if(!armed||e.LeftButton!=MouseButtonState.Pressed)return;var delta=e.GetPosition(button)-start;if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;armed=false;dragged=true;try{var data=new DataObject(DataFormats.FileDrop,item.Files);DragDrop.DoDragDrop(button,data,DragDropEffects.Copy);}catch(Exception error)when(error is ExternalException or InvalidOperationException){errorKey=item.Key;RefreshClipboard();}e.Handled=true;};
    button.PreviewMouseLeftButtonUp+=(_,e)=>{armed=false;if(dragged){button.ReleaseMouseCapture();e.Handled=true;}};
    AutomationProperties.SetHelpText(button,"Drag to export · Click to copy");
   }
   var card=new Grid{Margin=new Thickness(0,0,3,8),Background=Brushes.Transparent};
   button.Margin=new Thickness(0);button.Padding=new Thickness(9,6,35,6);card.Children.Add(button);
   var remove=Button("\u00D7",()=>{if(copiedKey==item.Key)copiedKey=null;if(errorKey==item.Key)errorKey=null;clipboard.Remove(item);},"Remove: "+item.Label);
   remove.Width=24;remove.Height=24;remove.FontSize=16;remove.Padding=new Thickness(0);remove.HorizontalAlignment=HorizontalAlignment.Right;remove.VerticalAlignment=VerticalAlignment.Center;remove.Margin=new Thickness(0,0,5,0);remove.Background=new SolidColorBrush(Color.FromRgb(40,41,47));remove.Foreground=PanelUi.Muted;remove.Opacity=0;remove.IsHitTestVisible=false;
   void RevealRemove(){bool visible=card.IsMouseOver||card.IsKeyboardFocusWithin;remove.Opacity=visible?1:0;remove.IsHitTestVisible=visible;}
   card.MouseEnter+=(_,_)=>RevealRemove();card.MouseLeave+=(_,_)=>RevealRemove();card.IsKeyboardFocusWithinChanged+=(_,_)=>RevealRemove();
   card.Children.Add(remove);entries.Children.Add(card);
  }
  scroll.ScrollToVerticalOffset(offset);
 }
 void Copy(CopyItem item){try{write(item);copiedKey=item.Key;errorKey=null;}catch(Exception error)when(error is ExternalException or InvalidOperationException or ArgumentException or IOException){copiedKey=null;errorKey=item.Key;}RefreshClipboard();}
 public void Dispose(){musicPane?.Dispose();if(clipboard!=null)clipboard.Changed-=RefreshClipboard;}
}
