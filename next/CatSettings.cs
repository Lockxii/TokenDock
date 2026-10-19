using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Threading;
namespace TokenDock;

sealed class CatSettings:Border {
 public CatSettings(Preferences draft){
  CornerRadius=new CornerRadius(12);Background=new SolidColorBrush(Color.FromRgb(25,26,32));Padding=new Thickness(14);Margin=new Thickness(0,0,0,8);
  Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse("""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
<Style TargetType="Slider"><Setter Property="Height" Value="22"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid><Border Height="3" CornerRadius="2" Background="#42444F"/><Track x:Name="PART_Track"><Track.Thumb><Thumb Width="11" Height="11"><Thumb.Template><ControlTemplate TargetType="Thumb"><Ellipse Fill="#9ADDBD"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter Property="Opacity" Value="0.7"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
""");
  var stack=new StackPanel();Child=stack;
  var preview=new CatLivePreview(draft);var header=new DockPanel();DockPanel.SetDock(preview,Dock.Right);header.Children.Add(preview);header.Children.Add(new TextBlock{Text="Chat · RAM",Foreground=PanelUi.Text,FontSize=12,VerticalAlignment=VerticalAlignment.Center});stack.Children.Add(header);
  var orange=Add("Orange",2,100,draft.CatOrange," %");var red=Add("Rouge",1,draft.CatOrange-1,draft.CatRed," %");var speed=Add("Vitesse",.25,2,draft.CatSpeed," ×",.05);var simulated=Add("RAM simulée",0,100,25," %");
  void Update(){draft.CatOrange=orange.Value;red.Maximum=orange.Value-1;draft.CatRed=red.Value;draft.CatSpeed=speed.Value;preview.Update(simulated.Value);}
  orange.ValueChanged+=(_,_)=>Update();red.ValueChanged+=(_,_)=>Update();speed.ValueChanged+=(_,_)=>Update();simulated.ValueChanged+=(_,_)=>Update();Update();
  Slider Add(string name,double min,double max,double value,string unit,double tick=1){
   var row=new Grid{Margin=new Thickness(0,6,0,0)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(90)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});
   row.Children.Add(new TextBlock{Text=name,FontSize=10,Foreground=PanelUi.Muted,VerticalAlignment=VerticalAlignment.Center});
   var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=tick,IsSnapToTickEnabled=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};AutomationProperties.SetName(slider,name);Grid.SetColumn(slider,1);row.Children.Add(slider);
   var label=new TextBlock{Text=value.ToString("0.##")+unit,FontSize=10,Foreground=PanelUi.Text,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(label,2);row.Children.Add(label);slider.ValueChanged+=(_,_)=>label.Text=slider.Value.ToString("0.##")+unit;stack.Children.Add(row);return slider;
  }
 }
}
sealed class CatLivePreview:FrameworkElement {
 readonly NotchSurface renderer=new(){Running=false};readonly Preferences draft;readonly DispatcherTimer timer=new();double available=25;
 public CatLivePreview(Preferences preferences){draft=preferences;Width=52;Height=42;timer.Tick+=(_,_)=>{renderer.AdvanceCat();InvalidateVisual();};Loaded+=(_,_)=>{if(SystemParameters.ClientAreaAnimation)timer.Start();};Unloaded+=(_,_)=>timer.Stop();IsVisibleChanged+=(_,_)=>{if(IsVisible&&IsLoaded&&SystemParameters.ClientAreaAnimation)timer.Start();else timer.Stop();};}
 public void Update(double free){available=free;renderer.Configure(draft);var ram=new MemoryInfo(10000,(ulong)(available*100));renderer.Update([],ram);timer.Interval=TimeSpan.FromMilliseconds(Math.Clamp(1000/(8*ram.CyclesPerSecond*draft.CatSpeed),30,800));InvalidateVisual();}
 protected override void OnRender(DrawingContext context)=>renderer.Cat(context,new Rect(7,6,36,32));
}
