using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
namespace TokenDock;

sealed record CameraFrame(int Width,int Height,byte[] Pixels) {
 public static (int Width,int Height,int Bytes) Header(byte[] data){
  int w=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0,4)),h=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4,4)),n=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8,4));
  if(w<=0||h<=0||w>1920||h>1080||n!=(long)w*h*4)throw new IOException("Camera returned an invalid frame.");return(w,h,n);
 }
 public Bitmap Bitmap(){var bitmap=new WriteableBitmap(new PixelSize(Width,Height),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque);using(var target=bitmap.Lock())for(int y=0;y<Height;y++)Marshal.Copy(Pixels,y*Width*4,target.Address+y*target.RowBytes,Width*4);return bitmap;}
 public byte[] RoundPng(){
  using var bitmap=new SKBitmap(new SKImageInfo(Width,Height,SKColorType.Bgra8888,SKAlphaType.Opaque));Marshal.Copy(Pixels,0,bitmap.GetPixels(),Pixels.Length);
  int side=Math.Min(640,Math.Min(Width,Height));using var surface=SKSurface.Create(new SKImageInfo(side,side,SKColorType.Bgra8888,SKAlphaType.Premul));
  surface.Canvas.Clear(SKColors.Transparent);using var circle=new SKPath();circle.AddCircle(side/2f,side/2f,side/2f);surface.Canvas.ClipPath(circle,SKClipOperation.Intersect,true);
  float crop=Math.Min(Width,Height);var source=SKRect.Create((Width-crop)/2,(Height-crop)/2,crop,crop);
  surface.Canvas.DrawBitmap(bitmap,source,SKRect.Create(side,side));using var image=surface.Snapshot();using var png=image.Encode(SKEncodedImageFormat.Png,100);return png.ToArray();
 }
}
sealed class Camera:IDisposable {
 CancellationTokenSource? session;Task? running;int generation;bool disposed;
 readonly string helper;readonly bool fixture;
 public CameraFrame? Frame{get;private set;}public string Status{get;private set;}="Opening camera…";
 public event Action? Changed;
 internal void SetFixture(CameraFrame frame){if(!fixture)throw new InvalidOperationException("Fixture capture only.");Frame=frame;Status="Synthetic preview";}
 internal static string HelperPath=>OperatingSystem.IsMacOS()?Path.Combine(AppContext.BaseDirectory,"native","TokenDockCamera.app","Contents","MacOS","tokendock-camera"):Path.Combine(AppContext.BaseDirectory,"native","tokendock-camera");
 public Camera(bool fixture=false,string? helper=null){this.fixture=fixture;this.helper=helper??HelperPath;}
 public void SetActive(bool active){
  if(disposed||active==(session!=null))return;
  generation++;session?.Cancel();session=null;Frame=null;
  if(!active){Changed?.Invoke();return;}
  var cancel=new CancellationTokenSource();session=cancel;int version=generation;Status="Opening camera…";
  // Never overlap two native capture sessions, even during rapid retargeting.
  running=CaptureAfter(running,version,cancel);Changed?.Invoke();
 }
 async Task CaptureAfter(Task? previous,int version,CancellationTokenSource cancel){
  try{
   if(previous!=null)await previous;
   cancel.Token.ThrowIfCancellationRequested();
   if(!File.Exists(helper))throw new IOException("Camera adapter is missing from this build.");
   var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(fixture?"--self-test":"--capture");
   using var process=new Process{StartInfo=start};if(!process.Start())throw new IOException("Camera could not start.");
   using var registration=cancel.Token.Register(()=>Terminate(process));
   var errors=ReadError(process.StandardError,cancel.Token);
   try{
    var stream=process.StandardOutput.BaseStream;byte[] header=new byte[12];
    while(!cancel.IsCancellationRequested){
     await stream.ReadExactlyAsync(header,cancel.Token);var(w,h,n)=CameraFrame.Header(header);byte[] bytes=new byte[n];await stream.ReadExactlyAsync(bytes,cancel.Token);
     var frame=new CameraFrame(w,h,bytes);
     // Backpressure: at most one frame can be awaiting UI dispatch.
     await Dispatcher.UIThread.InvokeAsync(()=>{if(!disposed&&version==generation&&!cancel.IsCancellationRequested){Frame=frame;Status="Live";Changed?.Invoke();}});
    }
   }catch(EndOfStreamException){if(!cancel.IsCancellationRequested){await process.WaitForExitAsync(cancel.Token);var error=await errors;throw new IOException(string.IsNullOrWhiteSpace(error)?"Camera stream ended.":error);}}
   finally{Terminate(process);await process.WaitForExitAsync(CancellationToken.None);try{await errors;}catch(OperationCanceledException){}}
  }catch(Exception e)when(e is IOException or System.ComponentModel.Win32Exception or OperationCanceledException){
   await Dispatcher.UIThread.InvokeAsync(()=>{if(!disposed&&version==generation&&!cancel.IsCancellationRequested){Status=e.Message;Frame=null;Changed?.Invoke();}});
  }finally{await Dispatcher.UIThread.InvokeAsync(()=>{if(ReferenceEquals(session,cancel))session=null;});cancel.Dispose();}
 }
 static async Task<string> ReadError(StreamReader reader,CancellationToken token){char[] buffer=new char[1024];string text="";int n;while((n=await reader.ReadAsync(buffer,token))>0)if(text.Length<8192)text+=new string(buffer,0,Math.Min(n,8192-text.Length));return text.Trim();}
 static void Terminate(Process process){try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}}
 public async Task<CopyItem> Photo(){
  var frame=Frame??throw new IOException("Wait for a camera frame.");
  var png=await Task.Run(frame.RoundPng);
  var pictures=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);if(string.IsNullOrWhiteSpace(pictures))pictures=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Pictures");
  var folder=Path.Combine(pictures,"TokenDock");Directory.CreateDirectory(folder);string path=Path.Combine(folder,$"Photo-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.png");
  await using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))await file.WriteAsync(png);
  return CopyItem.FromPng(png,[path])??throw new IOException("Could not encode the photo.");
 }
 public void Dispose(){if(disposed)return;SetActive(false);disposed=true;}
 internal Task Stopped=>running??Task.CompletedTask;
}
