using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
namespace TokenDock;

sealed class MusicPane:Grid,IDisposable {
 readonly MusicModule music;readonly Image cover=new(){Stretch=Stretch.UniformToFill};readonly Image fallback=ModuleIcons.Image("music",34);
 readonly Border artwork=new(){Background=new SolidColorBrush(Color.FromRgb(25,27,32)),CornerRadius=new CornerRadius(12)};
 readonly TextBlock title=Label(16,PanelUi.Text),artist=Label(11,PanelUi.Muted),elapsed=Label(9,PanelUi.Muted),duration=Label(9,PanelUi.Muted);
 readonly Slider seek=new(){Minimum=0,Maximum=1,IsMoveToPointEnabled=true,SmallChange=.02,LargeChange=.1,Height=16,VerticalAlignment=VerticalAlignment.Center};
 readonly Button previous,play,next;readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(200)};
 bool active,editing,updating,disposed;bool? renderedPlaying;
 internal bool TimerRunning=>timer.IsEnabled;
 const string SliderTheme="""
 <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Slider">
 <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid Background="Transparent"><Track x:Name="PART_Track">
 <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="#8EDDB6" Height="3" CornerRadius="1.5"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
 <Track.Thumb><Thumb Width="10" Height="16"><Thumb.Template><ControlTemplate TargetType="Thumb"><Grid Background="Transparent"><Ellipse Width="7" Height="7" Fill="#8EDDB6"/></Grid></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
 <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="#30313A" Height="3" CornerRadius="1.5"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
 </Track><Border x:Name="Focus" Height="2" VerticalAlignment="Bottom" Background="#8EDDB6" Margin="0,0,0,-2" Visibility="Collapsed"/></Grid><ControlTemplate.Triggers><MultiTrigger><MultiTrigger.Conditions><Condition Property="IsKeyboardFocused" Value="True"/><Condition Property="IsMouseOver" Value="False"/></MultiTrigger.Conditions><Setter TargetName="Focus" Property="Visibility" Value="Visible"/></MultiTrigger><Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
 </Style>
 """;
 public MusicPane(MusicModule music){
  this.music=music;ClipToBounds=true;Resources=PanelUi.Theme();
  Resources[typeof(Button)]=(Style)XamlReader.Parse("""
  <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button"><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="Body" Background="{TemplateBinding Background}" CornerRadius="20" Padding="{TemplateBinding Padding}" BorderBrush="Transparent" BorderThickness="1"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Body" Property="Background" Value="#34353C"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Body" Property="BorderBrush" Value="#8EDDB6"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter TargetName="Body" Property="Opacity" Value="0.65"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Body" Property="Opacity" Value="0.35"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
  """);title.FontWeight=FontWeights.SemiBold;artist.Margin=new Thickness(0,3,0,0);
  var images=new Grid();images.Children.Add(cover);images.Children.Add(fallback);artwork.Child=images;artwork.SizeChanged+=(_,_)=>artwork.Clip=new RectangleGeometry(new Rect(0,0,artwork.ActualWidth,artwork.ActualHeight),12,12);Children.Add(artwork);
  var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center};labels.Children.Add(title);labels.Children.Add(artist);Children.Add(labels);
  var controls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};previous=Transport("previous","Piste précédente",()=>_=music.Command("previous"));play=Transport("play","Lecture ou pause",()=>_=music.Command("toggle"));next=Transport("next","Piste suivante",()=>_=music.Command("next"));controls.Children.Add(previous);controls.Children.Add(play);controls.Children.Add(next);Children.Add(controls);
  var timeline=new Grid();timeline.RowDefinitions.Add(new RowDefinition{Height=new GridLength(16)});timeline.RowDefinitions.Add(new RowDefinition{Height=new GridLength(12)});seek.Style=(Style)XamlReader.Parse(SliderTheme);AutomationProperties.SetName(seek,"Position de lecture");timeline.Children.Add(seek);var times=new Grid();times.Children.Add(elapsed);duration.HorizontalAlignment=HorizontalAlignment.Right;times.Children.Add(duration);SetRow(times,1);timeline.Children.Add(times);Children.Add(timeline);
  void Layout(){bool narrow=ActualWidth<250;RowDefinitions.Clear();ColumnDefinitions.Clear();foreach(var element in new FrameworkElement[]{artwork,labels,controls,timeline}){SetRowSpan(element,1);SetColumnSpan(element,1);SetColumn(element,0);SetRow(element,0);}
   if(narrow){foreach(double height in new[]{124d,46,32,44})RowDefinitions.Add(new RowDefinition{Height=new GridLength(height)});artwork.Width=artwork.Height=112;artwork.HorizontalAlignment=HorizontalAlignment.Center;artwork.VerticalAlignment=VerticalAlignment.Top;SetRow(labels,1);SetRow(controls,3);SetRow(timeline,2);controls.HorizontalAlignment=HorizontalAlignment.Center;title.TextAlignment=artist.TextAlignment=TextAlignment.Center;}
   else{foreach(double height in new[]{40d,28,32})RowDefinitions.Add(new RowDefinition{Height=new GridLength(height)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(92)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});ColumnDefinitions.Add(new ColumnDefinition());artwork.Width=artwork.Height=92;artwork.VerticalAlignment=VerticalAlignment.Center;SetRowSpan(artwork,3);SetColumn(labels,2);SetColumn(controls,2);SetRow(controls,2);controls.HorizontalAlignment=HorizontalAlignment.Center;SetRow(timeline,1);SetColumn(timeline,2);title.TextAlignment=artist.TextAlignment=TextAlignment.Left;}
   play.Width=narrow?40:36;play.Height=narrow?40:30;previous.Height=next.Height=narrow?36:30;

  }
  SizeChanged+=(_,_)=>Layout();Layout();
  seek.PreviewMouseLeftButtonDown+=(_,_)=>{seek.FocusVisualStyle=null;if(seek.IsEnabled)editing=true;};seek.PreviewMouseLeftButtonUp+=(_,_)=>CommitSeek();seek.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler((_,e)=>{if(e.Canceled){editing=false;RefreshPosition();}else CommitSeek();}));seek.LostMouseCapture+=(_,_)=>{if(editing){editing=false;RefreshPosition();}};
  seek.ValueChanged+=(_,_)=>{if(!updating&&editing)ShowTimes(seek.Value);};seek.KeyUp+=(_,e)=>{if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageDown or Key.PageUp){editing=true;CommitSeek();e.Handled=true;}};
  timer.Tick+=(_,_)=>RefreshPosition();IsVisibleChanged+=(_,_)=>UpdateTimer();music.Changed+=Refresh;Refresh();
 }
 static TextBlock Label(double size,Brush brush)=>new(){FontSize=size,Foreground=brush,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis};
 static Button Transport(string icon,string name,Action action){var button=PanelUi.Button("",action);button.Content=ModuleIcons.Transport(icon);button.Width=36;button.MinHeight=30;button.Background=icon=="play"?new SolidColorBrush(Color.FromRgb(37,38,44)):Brushes.Transparent;button.Padding=new Thickness(6);button.Margin=new Thickness(4,0,4,0);AutomationProperties.SetName(button,name);return button;}
 void Refresh(){if(disposed)return;title.Text=music.Title;artist.Text=string.IsNullOrWhiteSpace(music.Artist)||music.Status.Contains("indisponible")||music.Status.Contains("non pris")?music.Status:music.Artist;cover.Source=music.Artwork;fallback.Visibility=music.Artwork==null?Visibility.Visible:Visibility.Collapsed;previous.IsEnabled=music.CanPrevious;play.IsEnabled=music.CanPlay;next.IsEnabled=music.CanNext;if(renderedPlaying!=music.Playing){renderedPlaying=music.Playing;play.Content=ModuleIcons.Transport(music.Playing?"pause":"play");AutomationProperties.SetName(play,music.Playing?"Mettre en pause":"Lancer la lecture");}seek.IsEnabled=music.CanSeek;RefreshPosition();UpdateTimer();}
 void ShowTimes(double fraction){var span=music.Timeline.Duration;if(span<=TimeSpan.Zero){elapsed.Text=duration.Text="—:—";return;}elapsed.Text=MusicTimeline.Clock(TimeSpan.FromTicks((long)((decimal)span.Ticks*(decimal)Math.Clamp(fraction,0,1))));duration.Text=MusicTimeline.Clock(span);}
 void RefreshPosition(){if(disposed||editing)return;var t=music.Timeline;double fraction=t.Duration>TimeSpan.Zero?(t.At(DateTimeOffset.UtcNow)-t.Start).TotalSeconds/t.Duration.TotalSeconds:0;updating=true;seek.Value=Math.Clamp(fraction,0,1);updating=false;ShowTimes(fraction);}
 void CommitSeek(){if(!editing)return;editing=false;if(music.CanSeek)_=music.Seek(seek.Value);}
 public void SetActive(bool value){active=value;if(!value)editing=false;UpdateTimer();}
 void UpdateTimer(){if(!disposed&&active&&IsVisible&&music.Playing&&music.Timeline.Duration>TimeSpan.Zero){RefreshPosition();timer.Start();}else timer.Stop();}
 public void Dispose(){disposed=true;timer.Stop();music.Changed-=Refresh;}
}
