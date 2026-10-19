using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace TokenDock;

static class PanelUi {
 public static readonly Brush Text=Brushes.WhiteSmoke,Muted=new SolidColorBrush(Color.FromRgb(151,151,166)),Mint=new SolidColorBrush(Color.FromRgb(142,221,182));
 public static ResourceDictionary Theme()=>(ResourceDictionary)XamlReader.Parse("""
 <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="Button"><Setter Property="Background" Value="#25252D"/><Setter Property="Foreground" Value="#EEEEF4"/><Setter Property="Padding" Value="12,7"/><Setter Property="Margin" Value="0,0,6,6"/><Setter Property="MinHeight" Value="30"/>
   <Setter Property="HorizontalContentAlignment" Value="Center"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="B" Background="{TemplateBinding Background}" CornerRadius="7" Padding="{TemplateBinding Padding}" BorderThickness="1" BorderBrush="Transparent"><ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#373741"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="B" Property="BorderBrush" Value="#8EDDB6"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="B" Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="CheckBox"><Setter Property="Foreground" Value="#EEEEF4"/><Setter Property="Margin" Value="0,8,0,8"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal"><Border x:Name="Box" Background="#292932" CornerRadius="5" Width="22" Height="22" BorderBrush="#4B4B58" BorderThickness="1"><TextBlock x:Name="Mark" Text="✓" Foreground="#111116" Visibility="Collapsed" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ContentPresenter Margin="10,0,0,0" VerticalAlignment="Center"/></StackPanel><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="Box" Property="Background" Value="#8EDDB6"/><Setter TargetName="Mark" Property="Visibility" Value="Visible"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Box" Property="BorderBrush" Value="White"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="TextBox"><Setter Property="Background" Value="#25252D"/><Setter Property="Foreground" Value="#EEEEF4"/><Setter Property="CaretBrush" Value="White"/><Setter Property="BorderBrush" Value="#4B4B58"/><Setter Property="Padding" Value="7"/><Setter Property="Margin" Value="0,0,6,6"/></Style>
  <Style TargetType="ScrollBar"><Setter Property="Width" Value="8"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#4B4B58" CornerRadius="2.5" Width="5" HorizontalAlignment="Center"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter></Style>
 </ResourceDictionary>
 """);
 public static TextBlock Label(string value,double size=12,Brush? color=null)=>new(){Text=value,FontSize=size,Foreground=color??Text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};
 public static Button Button(string label,Action action){var button=new Button{Content=label};button.Click+=(_,_)=>action();return button;}
 public static CheckBox Toggle(string label,bool value,Action<bool> change){var box=new CheckBox{Content=label,IsChecked=value};box.Checked+=(_,_)=>change(true);box.Unchecked+=(_,_)=>change(false);return box;}
 public static void Heading(Panel panel,string label){var text=Label(label,14,Mint);text.Margin=new Thickness(0,18,0,4);panel.Children.Add(text);}
}

sealed class ToolPanelWindow:Window {
 public ToolPanelWindow(string title,UIElement body){
  Title="TokenDock · "+title;Width=540;SizeToContent=SizeToContent.Height;MaxHeight=Math.Max(340,SystemParameters.WorkArea.Height-40);MinWidth=380;MinHeight=340;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Resources=PanelUi.Theme();
  Icon=BitmapFrame.Create(new Uri(Path.Combine(AppContext.BaseDirectory,"assets","tokendock.ico")));
  var root=new DockPanel();var header=new DockPanel{Margin=new Thickness(24,20,18,12)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  var close=PanelUi.Button("×",Close);close.Width=30;close.Height=30;close.MinHeight=30;close.Padding=new Thickness(0);close.Background=Brushes.Transparent;close.Margin=new Thickness(0);System.Windows.Automation.AutomationProperties.SetName(close,"Close");DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);var text=PanelUi.Label(title,20);text.FontWeight=FontWeights.SemiBold;text.MouseLeftButtonDown+=(_,_)=>DragMove();header.Children.Add(text);
  root.Children.Add(new ScrollViewer{Content=body,MaxHeight=Math.Max(240,SystemParameters.WorkArea.Height-140),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(24,0,18,22)});
  Content=new Border{Background=new SolidColorBrush(Color.FromRgb(20,20,25)),BorderBrush=new SolidColorBrush(Color.FromRgb(51,51,61)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Child=root};
  KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape)Close();};
 }
}
