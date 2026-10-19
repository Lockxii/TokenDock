using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Graphics.Imaging;
namespace TokenDock;

sealed class CameraPane:Grid,IDisposable {
 readonly Action<string,BitmapSource>? photoSaved;
 readonly Image preview=new(){Stretch=Stretch.UniformToFill};readonly TextBlock status=new(){Text="Caméra au repos",Foreground=PanelUi.Muted,FontSize=10,TextWrapping=TextWrapping.Wrap};readonly Button photo;
 readonly SemaphoreSlim gate=new(1);MediaCapture? capture;MediaFrameReader? reader;bool desired,disposed;int pending;long lastFrame;BitmapSource? latest;
 public CameraPane(Action<string,BitmapSource>? photoSaved=null){
  this.photoSaved=photoSaved;
  Resources=PanelUi.Theme();RowDefinitions.Add(new RowDefinition());RowDefinitions.Add(new RowDefinition{Height=new GridLength(54)});
  preview.Width=148;preview.Height=148;preview.Clip=new EllipseGeometry(new Point(74,74),74,74);
  Children.Add(new Border{Width=152,Height=152,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Background=new SolidColorBrush(Color.FromRgb(18,19,24)),BorderBrush=new SolidColorBrush(Color.FromRgb(45,47,54)),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(76),Child=preview});
  var footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});footer.ColumnDefinitions.Add(new ColumnDefinition());SetRow(footer,1);Children.Add(footer);
  photo=new Button{Width=44,Height=44,Margin=new Thickness(0),Padding=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Center,IsEnabled=false,Cursor=System.Windows.Input.Cursors.Hand,ToolTip="Prendre une photo"};photo.Click+=(_,_)=>SavePhoto();System.Windows.Automation.AutomationProperties.SetName(photo,"Prendre une photo");
  photo.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button"><Grid x:Name="Shutter"><Ellipse Stroke="#C8D3CD" StrokeThickness="1.5"/><Ellipse x:Name="Disc" Fill="#F2F5F3" Margin="5"/></Grid><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Disc" Property="Fill" Value="#9ADDBD"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter TargetName="Disc" Property="Margin" Value="8"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Shutter" Property="Opacity" Value="0.35"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Disc" Property="Fill" Value="#9ADDBD"/></Trigger></ControlTemplate.Triggers></ControlTemplate>
""");Grid.SetColumn(photo,1);footer.Children.Add(photo);status.VerticalAlignment=VerticalAlignment.Center;status.FontSize=9;status.Margin=new Thickness(0,0,8,0);footer.Children.Add(status);
  Unloaded+=(_,_)=>SetActive(false);
 }
 public void SetActive(bool active){active&=!disposed;if(desired==active)return;desired=active;if(!active){photo.IsEnabled=false;latest=null;preview.Source=null;} _=Reconcile();}
 async Task Reconcile(){
  await gate.WaitAsync();
  try{
   if(!desired||disposed){await Stop();return;}
   if(reader!=null)return;status.Text="Ouverture…";
   capture=new MediaCapture();await capture.InitializeAsync(new MediaCaptureInitializationSettings{StreamingCaptureMode=StreamingCaptureMode.Video,MemoryPreference=MediaCaptureMemoryPreference.Cpu,SharingMode=MediaCaptureSharingMode.SharedReadOnly});
   if(!desired||disposed){await Stop();return;}
   var source=capture.FrameSources.Values.FirstOrDefault(s=>s.Info.SourceKind==MediaFrameSourceKind.Color)??throw new IOException("Aucune caméra disponible.");
   var format=source.CurrentFormat.VideoFormat;uint width=Math.Min(640u,format.Width),height=(uint)Math.Max(1,Math.Round(width*(double)format.Height/Math.Max(1u,format.Width)));reader=await capture.CreateFrameReaderAsync(source,MediaEncodingSubtypes.Bgra8,new BitmapSize{Width=width,Height=height});reader.AcquisitionMode=MediaFrameReaderAcquisitionMode.Realtime;reader.FrameArrived+=Frame;
   if(!desired||disposed){await Stop();return;}
   if(await reader.StartAsync()!=MediaFrameReaderStartStatus.Success)throw new IOException("Caméra occupée ou indisponible.");
   if(!desired||disposed){await Stop();return;}status.Text="● En direct";
  }catch(Exception e){await Stop();if(desired&&!disposed){status.Text=e is UnauthorizedAccessException?"Autorise la caméra dans les paramètres Windows.":"Caméra indisponible. Ferme puis réouvre ce panneau.";status.ToolTip=e.Message;}}
  finally{gate.Release();}
 }
 void Frame(MediaFrameReader sender,MediaFrameArrivedEventArgs args){
  if(!desired||disposed||Environment.TickCount64-Interlocked.Read(ref lastFrame)<66||Interlocked.Exchange(ref pending,1)!=0)return;
  try{
   using var frame=sender.TryAcquireLatestFrame();using var bitmap=frame?.VideoMediaFrame?.SoftwareBitmap;if(bitmap==null){Interlocked.Exchange(ref pending,0);return;}
   using var converted=SoftwareBitmap.Convert(bitmap,BitmapPixelFormat.Bgra8,BitmapAlphaMode.Ignore);int w=converted.PixelWidth,h=converted.PixelHeight;if(w<=0||h<=0||(long)w*h>1920*1080)throw new IOException("Image trop grande");
   byte[] pixels=new byte[w*h*4];converted.CopyToBuffer(pixels.AsBuffer());var image=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,w*4);image.Freeze();Interlocked.Exchange(ref lastFrame,Environment.TickCount64);
   Dispatcher.BeginInvoke(()=>{try{if(desired&&!disposed&&ReferenceEquals(sender,reader)){latest=image;preview.Source=SquarePhoto(image);photo.IsEnabled=true;}}finally{Interlocked.Exchange(ref pending,0);}});
  }catch{Interlocked.Exchange(ref pending,0);}
 }
 async Task Stop(){var old=reader;reader=null;if(old!=null){old.FrameArrived-=Frame;try{await old.StopAsync();}catch{}finally{old.Dispose();}}capture?.Dispose();capture=null;latest=null;preview.Source=null;photo.IsEnabled=false;}
 internal static BitmapSource SquarePhoto(BitmapSource source){int size=Math.Min(source.PixelWidth,source.PixelHeight);var square=new CroppedBitmap(source,new Int32Rect((source.PixelWidth-size)/2,(source.PixelHeight-size)/2,size,size));square.Freeze();return square;}
 internal static BitmapSource RoundPhoto(BitmapSource source){
  source=SquarePhoto(source);
  int size=Math.Min(source.PixelWidth,source.PixelHeight);var visual=new DrawingVisual();
  using(var drawing=visual.RenderOpen()){drawing.PushClip(new EllipseGeometry(new Point(size/2d,size/2d),size/2d,size/2d));drawing.DrawImage(source,new Rect((size-source.PixelWidth)/2d,(size-source.PixelHeight)/2d,source.PixelWidth,source.PixelHeight));drawing.Pop();}
  var result=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
 }
 void SavePhoto(){if(latest==null||!desired)return;try{string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"TokenDock");Directory.CreateDirectory(folder);string path=Path.Combine(folder,$"Photo-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");var rounded=RoundPhoto(latest);var encoder=new PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rounded));using(var file=new FileStream(path,FileMode.CreateNew))encoder.Save(file);photoSaved?.Invoke(path,rounded);status.Text="✓ Photo enregistrée";status.ToolTip=path;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){status.Text="Enregistrement impossible";status.ToolTip=e.Message;}}
 public void Dispose(){disposed=true;SetActive(false);}
}
