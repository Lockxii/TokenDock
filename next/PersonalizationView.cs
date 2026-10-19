using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
namespace TokenDock;

sealed class PersonalizationView:StackPanel {
 readonly Preferences draft;readonly Action<Preferences> apply;readonly Action close;readonly Func<Meter[]> snapshot;
 readonly Dictionary<string,ImageSource> icons=new();readonly Dictionary<string,Button> previewButtons=new();bool vertical=true,advancedOpen;string? dragging;Point start;
 internal Preferences Draft=>draft.Copy();
 public PersonalizationView(Preferences current,Action<Preferences> apply,Action close,Func<Meter[]> snapshot){draft=current.Copy();draft.Normalize();this.apply=apply;this.close=close;this.snapshot=snapshot;Resources=EditorTheme();Build();}
 static Brush Brush(byte r,byte g,byte b)=>new SolidColorBrush(Color.FromRgb(r,g,b));
 static ResourceDictionary EditorTheme(){var theme=PanelUi.Theme();var custom=(ResourceDictionary)XamlReader.Parse("""
 <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Style x:Key="KeyboardFocus"><Setter Property="Control.Template"><Setter.Value><ControlTemplate><Border BorderBrush="#8EDDB6" BorderThickness="1" CornerRadius="10" Margin="2"/></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="Button"><Setter Property="Foreground" Value="#ECECF1"/><Setter Property="Background" Value="#25262E"/><Setter Property="Padding" Value="12,7"/><Setter Property="Margin" Value="0,0,6,0"/><Setter Property="FocusVisualStyle" Value="{StaticResource KeyboardFocus}"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="Body" Background="{TemplateBinding Background}" CornerRadius="12" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Body" Property="Opacity" Value="0.78"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter TargetName="Body" Property="Opacity" Value="0.55"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Body" Property="Opacity" Value="0.35"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="CheckBox"><Setter Property="Foreground" Value="#CDCED7"/><Setter Property="FontSize" Value="11"/><Setter Property="FocusVisualStyle" Value="{StaticResource KeyboardFocus}"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal"><Grid Width="28" Height="16"><Border x:Name="Track" Background="#41434E" CornerRadius="8"/><Ellipse x:Name="Knob" Width="12" Height="12" Fill="#F4F4F7" HorizontalAlignment="Left" Margin="2"/></Grid><ContentPresenter VerticalAlignment="Center" Margin="8,0,0,0"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Track" Property="Background" Value="#6CB58E"/><Setter TargetName="Knob" Property="HorizontalAlignment" Value="Right"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 </ResourceDictionary>
 """);foreach(var key in custom.Keys)theme[key]=custom[key];return theme;}
 string ItemName(string id)=>id switch{"music"=>"Musique","clipboard"=>"Copies","camera"=>"Cam\u00e9ra","cat"=>"Chat · RAM",_=>draft.Name(id)};
 FrameworkElement Icon(string id,double size=24){
  if(id=="cat")return new Image{Source=NotchSurface.CatPreview(),Width=size,Height=size,Stretch=Stretch.Uniform};
  if(id is "music" or "clipboard" or "camera")return ModuleIcons.Image(id,size);
  if(Preferences.Builtin.Contains(id)){if(!icons.TryGetValue(id,out var icon))icons[id]=icon=NotchSurface.Logo(id);return new Image{Source=icon,Width=size,Height=size};}
  return new TextBlock{Text=ItemName(id)[..1].ToUpperInvariant(),FontSize=size*.7,Foreground=PanelUi.Mint,HorizontalAlignment=HorizontalAlignment.Center};
 }
 Button ActionButton(string label,Action action){var b=PanelUi.Button(label,action);b.FontSize=11;b.MinHeight=30;return b;}
 void Changed(){draft.Normalize();Build();}
 internal void Move(string id,string? target,bool after=false){draft.MoveItem(id,target,after);Changed();if(Keyboard.Modifiers==ModifierKeys.Alt&&previewButtons.TryGetValue(id,out var button))button.Focus();}
 internal void Remove(string id){draft.ShowItem(id,false);Changed();}
 Button Tile(string id,bool inBar){
  var b=ActionButton("",()=>{if(!inBar){draft.ShowItem(id,!draft.IsShown(id));Changed();}});b.Tag=id;b.Uid=inBar?"rail":"library";b.Padding=new Thickness(inBar?0:8);b.Margin=new Thickness(4);b.Background=inBar?Brushes.Transparent:Brush(30,31,38);
  var content=new Grid();
  if(inBar){content.Children.Add(Icon(id));b.Width=44;b.Height=44;}
  else{b.Width=94;b.Height=74;content.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});content.RowDefinitions.Add(new RowDefinition{Height=new GridLength(18)});var icon=Icon(id,25);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.VerticalAlignment=VerticalAlignment.Center;content.Children.Add(icon);var label=new TextBlock{Text=ItemName(id),FontSize=10,Foreground=PanelUi.Text,TextTrimming=TextTrimming.CharacterEllipsis,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom};Grid.SetRow(label,1);content.Children.Add(label);var mark=new TextBlock{Text=draft.IsShown(id)?"•":"+",FontSize=12,Foreground=draft.IsShown(id)?PanelUi.Mint:PanelUi.Muted,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top};content.Children.Add(mark);}
  b.Content=content;AutomationProperties.SetName(b,ItemName(id));AutomationProperties.SetHelpText(b,inBar?"Glisser pour déplacer. Suppr pour retirer. Alt + flèche pour réordonner.":"Glisser vers la barre ou cliquer pour ajouter / retirer.");
  b.PreviewMouseLeftButtonDown+=(_,e)=>start=e.GetPosition(this);
  b.PreviewMouseMove+=(_,e)=>{if(e.LeftButton!=MouseButtonState.Pressed||dragging!=null)return;var p=e.GetPosition(this);if(Math.Abs(p.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(p.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;dragging=id;try{DragDrop.DoDragDrop(b,new DataObject("TokenDock.Layout",id),DragDropEffects.Move);}finally{dragging=null;}};
  b.KeyDown+=(_,e)=>{if(!inBar)return;if(e.Key==Key.Delete){Remove(id);e.Handled=true;return;}if(Keyboard.Modifiers!=ModifierKeys.Alt)return;int delta=e.Key is Key.Up or Key.Left?-1:e.Key is Key.Down or Key.Right?1:0;if(delta==0)return;var list=draft.Layout;int at=Array.IndexOf(list,id),to=at+delta;if(to>=0&&to<list.Length)Move(id,list[to],delta>0);e.Handled=true;};
  if(inBar){previewButtons[id]=b;DropZone(b,id);}return b;
 }
 void DropZone(FrameworkElement zone,string? target=null,bool remove=false){
  zone.AllowDrop=true;var idleBorder=(zone as Border)?.BorderBrush;
  bool Accept()=>dragging!=null;
  zone.DragOver+=(_,e)=>{e.Effects=Accept()?DragDropEffects.Move:DragDropEffects.None;if(zone is Button tile)tile.Background=Accept()?new SolidColorBrush(Color.FromRgb(37,61,48)):Brushes.Transparent;if(zone is Border border)border.BorderBrush=Accept()?PanelUi.Mint:Brushes.Transparent;e.Handled=true;};
  zone.DragLeave+=(_,_)=>{if(zone is Button tile)tile.Background=Brushes.Transparent;if(zone is Border border)border.BorderBrush=idleBorder;};
  zone.Drop+=(_,e)=>{if(!Accept())return;string id=dragging!;dragging=null;if(remove)Remove(id);else{var point=e.GetPosition(zone);Move(id,target,target!=null&&(vertical?point.Y>zone.ActualHeight/2:point.X>zone.ActualWidth/2));}e.Handled=true;};
 }
 void Build(){
  Children.Clear();previewButtons.Clear();Children.Add(PanelUi.Label("Ta barre. À ta façon.",13,PanelUi.Muted));
  var body=new Grid{Margin=new Thickness(0,6,0,0)};body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(212)});Children.Add(body);
  var previewColumn=new StackPanel();body.Children.Add(previewColumn);
  var orientations=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,0,10)};foreach(bool isVertical in new[]{true,false}){bool mode=isVertical;var b=ActionButton(mode?"Verticale":"Horizontale",()=>{vertical=mode;Build();});b.Foreground=vertical==mode?PanelUi.Text:PanelUi.Muted;b.Background=vertical==mode?Brush(43,44,53):Brushes.Transparent;b.Padding=new Thickness(14,6,14,6);orientations.Children.Add(b);}previewColumn.Children.Add(orientations);
  var frame=new Border{Height=vertical?Math.Clamp(draft.Layout.Length*36+26,82,260):88,Width=vertical?104:double.NaN,HorizontalAlignment=vertical?HorizontalAlignment.Center:HorizontalAlignment.Stretch,Margin=new Thickness(0,0,18,0),CornerRadius=new CornerRadius(20),Background=new LinearGradientBrush(Color.FromRgb(32,34,45),Color.FromRgb(21,22,28),60),BorderBrush=Brush(48,50,62),BorderThickness=new Thickness(1)};DropZone(frame);
  var rail=new StackPanel{Orientation=vertical?Orientation.Vertical:Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
  var visible=draft.Layout;
  if(draft.CounterAbove){var count=new TextBlock{Text=draft.Counter=="all"?"∑":ItemName(draft.Counter),FontSize=9,Foreground=PanelUi.Mint,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(3)};rail.Children.Add(count);}
  foreach(var id in visible){var tile=Tile(id,true);tile.Width=44;tile.Height=44;tile.Padding=new Thickness(0);tile.Margin=new Thickness(2);rail.Children.Add(tile);}
  if(visible.Length==0)rail.Children.Add(PanelUi.Label("+",24,PanelUi.Muted));
  var notch=new Border{Background=Brushes.Black,CornerRadius=new CornerRadius(22),Padding=new Thickness(7),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=new Viewbox{Child=rail,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,MaxWidth=340,MaxHeight=vertical?Math.Clamp(draft.Layout.Length*36+6,62,240):62}};frame.Child=notch;previewColumn.Children.Add(frame);
  var trash=new Border{Margin=new Thickness(0,8,18,0),Padding=new Thickness(10),CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Background=new SolidColorBrush(Color.FromRgb(28,28,34)),Child=new TextBlock{Text="−   Glisser ici pour retirer",FontSize=10,Foreground=PanelUi.Muted,HorizontalAlignment=HorizontalAlignment.Center}};DropZone(trash,remove:true);previewColumn.Children.Add(trash);
  var palette=new StackPanel();Grid.SetColumn(palette,1);body.Children.Add(palette);palette.Children.Add(PanelUi.Label("ÉLÉMENTS",10,PanelUi.Muted));var cards=new WrapPanel();foreach(var id in draft.Ids.Concat(new[]{"music","clipboard","camera","cat"}))cards.Children.Add(Tile(id,false));palette.Children.Add(cards);
  var options=new WrapPanel{Margin=new Thickness(14,12,14,12)};
  options.Children.Add(PanelUi.Toggle("Compteur",draft.CounterAbove,v=>{draft.CounterAbove=v;Changed();}));options.Children.Add(PanelUi.Toggle("Pochette",draft.MusicPreview,v=>{draft.MusicPreview=v;Changed();}));options.Children.Add(PanelUi.Toggle("Titre",draft.MusicTitle,v=>{draft.MusicTitle=v;Changed();}));foreach(FrameworkElement item in options.Children)item.Margin=new Thickness(0,0,20,0);Children.Add(new Border{Child=options,Background=Brush(25,26,32),CornerRadius=new CornerRadius(12),Margin=new Thickness(0,12,0,8)});
  if(draft.CounterAbove){var choices=new WrapPanel{Margin=new Thickness(0,0,0,8)};foreach(var id in new[]{"all"}.Concat(draft.Ids)){var button=ActionButton("",()=>{draft.Counter=id;Changed();});button.Content=id=="all"?new TextBlock{Text="∑",FontSize=18,Foreground=PanelUi.Text}:Icon(id,18);button.Width=38;button.Padding=new Thickness(7);button.Background=draft.Counter==id?new SolidColorBrush(Color.FromRgb(43,64,54)):Brushes.Transparent;AutomationProperties.SetName(button,"Compteur : "+draft.Name(id));choices.Children.Add(button);}Children.Add(choices);}
  Children.Add(new CatSettings(draft));
  var advanced=ActionButton("",()=>{advancedOpen=!advancedOpen;Build();});advanced.HorizontalAlignment=HorizontalAlignment.Left;advanced.Padding=new Thickness(11,8,11,8);advanced.Background=advancedOpen?Brush(43,45,55):Brush(30,31,38);AutomationProperties.SetName(advanced,"Réglages avancés");var advancedContent=new StackPanel{Orientation=Orientation.Horizontal};advancedContent.Children.Add(new TextBlock{Text="\uE713",FontFamily=new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),FontSize=13,Foreground=PanelUi.Muted,VerticalAlignment=VerticalAlignment.Center});advancedContent.Children.Add(new TextBlock{Text="Options",FontSize=11,Foreground=PanelUi.Text,Margin=new Thickness(7,0,0,0)});advanced.Content=advancedContent;var detail=new StackPanel{Margin=new Thickness(0,10,0,0),Visibility=advancedOpen?Visibility.Visible:Visibility.Collapsed};

  var sourceCard=new StackPanel();
  sourceCard.Children.Add(new TextBlock{Text="SOURCE PERSONNALIS\u00c9E",FontSize=9,Foreground=PanelUi.Muted,Margin=new Thickness(0,0,0,10)});
  var sourceRow=new Grid();sourceRow.ColumnDefinitions.Add(new ColumnDefinition());sourceRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var customName=new TextBox{Text="Mon outil",MaxLength=32,FontSize=12,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=PanelUi.Text,VerticalContentAlignment=VerticalAlignment.Center};AutomationProperties.SetName(customName,"Nom de la source");
  var nameFrame=new Border{Child=customName,CornerRadius=new CornerRadius(9),Background=Brush(34,35,42),Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,8,0)};sourceRow.Children.Add(nameFrame);
  var add=ActionButton("+ Connecter",()=>{var picker=new Microsoft.Win32.OpenFileDialog{Filter="Compteurs JSON (*.json)|*.json",CheckFileExists=true};if(picker.ShowDialog()!=true)return;string name=customName.Text.Trim();if(name.Length==0)name="Mon outil";var source=new CustomSource(Guid.NewGuid().ToString("N"),name,picker.FileName);draft.Sources=draft.Sources.Append(source).ToArray();draft.ShowItem(source.Id,true);Changed();});add.Margin=new Thickness(0);add.Padding=new Thickness(14,8,14,8);add.FontSize=11;add.Foreground=PanelUi.Mint;add.IsEnabled=draft.Sources.Length<3;add.ToolTip="Choisir un fichier JSON local : input, output, cache cumul\u00e9s";Grid.SetColumn(add,1);sourceRow.Children.Add(add);sourceCard.Children.Add(sourceRow);
  sourceCard.Children.Add(new TextBlock{Text="Fichier JSON local \u00b7 "+draft.Sources.Length+" / 3 sources",FontSize=10,Foreground=PanelUi.Muted,Margin=new Thickness(0,8,0,0)});
  foreach(var source in draft.Sources){var remove=ActionButton("\u00d7  "+source.Name,()=>{draft.Sources=draft.Sources.Where(s=>s.Id!=source.Id).ToArray();Changed();});remove.HorizontalAlignment=HorizontalAlignment.Left;remove.FontSize=11;AutomationProperties.SetName(remove,"Retirer la source "+source.Name);sourceCard.Children.Add(remove);}
  detail.Children.Add(new Border{Child=sourceCard,Background=Brush(25,26,32),CornerRadius=new CornerRadius(12),Padding=new Thickness(14),Margin=new Thickness(0,0,0,8)});
  var connections=new StackPanel();
  foreach(var meter in snapshot()){
   var line=new Grid{Margin=new Thickness(0,5,0,5)};line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(22)});line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(100)});line.ColumnDefinitions.Add(new ColumnDefinition());
   string id=meter.Name.Contains("Claude")?"claude":meter.Name.Contains("OpenCode")?"opencode":"codex";line.Children.Add(Icon(id,16));
   var name=new TextBlock{Text=meter.Name,FontSize=11,Foreground=PanelUi.Text,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)};Grid.SetColumn(name,1);line.Children.Add(name);
   var state=new TextBlock{Text=meter.Status,FontSize=10,Foreground=PanelUi.Muted,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,ToolTip=meter.Status};Grid.SetColumn(state,2);line.Children.Add(state);connections.Children.Add(line);
  }
  if(connections.Children.Count>0)detail.Children.Add(new Border{Child=connections,CornerRadius=new CornerRadius(12),Background=Brush(25,26,32),Padding=new Thickness(14,7,14,7),Margin=new Thickness(0,0,0,8)});
  Children.Add(detail);
  var status=PanelUi.Label("",11,PanelUi.Mint);status.Visibility=Visibility.Collapsed;Children.Add(status);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(ActionButton("Annuler",close));var save=ActionButton("Appliquer",()=>{try{draft.Normalize();draft.Save();apply(draft.Copy());close();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){status.Text="Enregistrement impossible.";status.Visibility=Visibility.Visible;}});save.Background=PanelUi.Mint;save.Foreground=Brush(16,27,21);save.FontWeight=FontWeights.SemiBold;save.Padding=new Thickness(22,8,22,8);actions.Children.Add(save);var footer=new DockPanel{Margin=new Thickness(0,4,0,0)};DockPanel.SetDock(advanced,Dock.Left);footer.Children.Add(advanced);footer.Children.Add(actions);Children.Add(footer);
 }

}

