using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
namespace TokenDock;

static class DockMenu {
 const string Theme="""
 <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="ContextMenu">
   <Setter Property="Background" Value="#141418"/><Setter Property="Foreground" Value="#ECECF1"/>
   <Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="12"/>
   <Setter Property="Width" Value="250"/><Setter Property="SnapsToDevicePixels" Value="True"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ContextMenu">
    <Border Background="{TemplateBinding Background}" BorderBrush="#323239" BorderThickness="1" CornerRadius="12" Padding="7">
     <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Cycle"/>
    </Border>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="MenuItem">
   <Setter Property="Background" Value="Transparent"/><Setter Property="Foreground" Value="#ECECF1"/>
   <Setter Property="Height" Value="34"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="MenuItem">
    <Border x:Name="Row" Background="{TemplateBinding Background}" CornerRadius="7" Padding="10,0">
     <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="25"/><ColumnDefinition Width="*"/><ColumnDefinition Width="18"/></Grid.ColumnDefinitions>
      <ContentPresenter ContentSource="Icon" VerticalAlignment="Center" TextElement.Foreground="#9B9BA8"/>
      <ContentPresenter Grid.Column="1" ContentSource="Header" RecognizesAccessKey="True" VerticalAlignment="Center"/>
      <TextBlock x:Name="Check" Grid.Column="2" Text="✓" Foreground="#8EDDB6" VerticalAlignment="Center" HorizontalAlignment="Right" Visibility="Collapsed"/>
     </Grid>
    </Border>
    <ControlTemplate.Triggers>
     <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Row" Property="Background" Value="#2A2A32"/></Trigger>
     <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Row" Property="Background" Value="#2A2A32"/></Trigger>
     <Trigger Property="IsChecked" Value="True"><Setter TargetName="Check" Property="Visibility" Value="Visible"/></Trigger>
     <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Row" Property="Opacity" Value="0.45"/></Trigger>
    </ControlTemplate.Triggers>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="Separator"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Separator">
   <Border Height="1" Background="#303037" Margin="10,6"/>
  </ControlTemplate></Setter.Value></Setter></Style>
 </ResourceDictionary>
 """;
 internal static ContextMenu Create(Func<string> currentEdge,Action<string> setEdge,Action reset,Action hide,Action quit,Action? customize=null,Action? music=null,Action? clipboard=null){
  var menu=new ContextMenu{Resources=(ResourceDictionary)XamlReader.Parse(Theme)};
  foreach(var (name,id,icon) in new[]{("En haut","top","↑"),("En bas","bottom","↓"),("À gauche","left","←"),("À droite","right","→")}){
   var item=new MenuItem{Header=name,Icon=icon,Tag=id,IsCheckable=true};
   item.Click+=(_,_)=>{setEdge(id);Refresh();};menu.Items.Add(item);
  }
  menu.Items.Add(new Separator{Style=(Style)menu.Resources[typeof(Separator)]});
  void Add(string name,string icon,Action action){var item=new MenuItem{Header=name,Icon=name=="Musique"?ModuleIcons.Image("music",19):name=="Presse-papiers"?ModuleIcons.Image("clipboard",19):icon};item.Click+=(_,_)=>action();menu.Items.Add(item);}
  if(customize!=null)Add("Personnaliser…","⚙",customize);if(music!=null)Add("Musique","♫",music);if(clipboard!=null)Add("Presse-papiers","▤",clipboard);
  Add("Réinitialiser les tokens","↺",reset);Add("Masquer","−",hide);Add("Quitter","×",quit);
  void Refresh(){foreach(var item in menu.Items.OfType<MenuItem>())if(item.Tag is string id)item.IsChecked=id==currentEdge();}
  menu.Opened+=(_,_)=>Refresh();Refresh();return menu;
 }
}
