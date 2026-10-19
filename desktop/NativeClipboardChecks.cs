using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
namespace TokenDock;
static class NativeClipboardChecks {
 public static async Task Run(Window window){
  if(Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated"||Environment.GetEnvironmentVariable("CI")!="true")throw new InvalidOperationException("Native clipboard checks require an isolated CI session.");
  var clipboard=window.Clipboard??throw new InvalidOperationException("No native clipboard.");
  const string text="TokenDock synthetic clipboard roundtrip";
  await clipboard.SetTextAsync(text);if(await clipboard.TryGetTextAsync()!=text)throw new InvalidOperationException("Native text roundtrip failed.");
  var frame=new CameraFrame(64,64,Enumerable.Repeat((byte)255,64*64*4).ToArray());var png=frame.RoundPng();
  var folder=Path.Combine(Path.GetTempPath(),"tokendock-native-copy-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  try{
   string path=Path.Combine(folder,"synthetic-photo.png");await File.WriteAllBytesAsync(path,png);var item=CopyItem.FromPng(png,[path])!;
   await clipboard.SetDataAsync(await item.Transfer(window));
   using var data=await clipboard.TryGetDataAsync()??throw new InvalidOperationException("Native transfer missing.");
   var files=await data.TryGetFilesAsync();if(files==null||!files.Any(file=>file.TryGetLocalPath()==path))throw new InvalidOperationException("Native image/file transfer lost its file URL.");
   using var bitmap=await data.TryGetBitmapAsync();if(bitmap==null||bitmap.PixelSize.Width!=64||bitmap.PixelSize.Height!=64)throw new InvalidOperationException("Native image roundtrip failed.");
   Console.WriteLine("PASS: native text, circular image and photo file-URL clipboard roundtrips in isolated CI session.");
  }finally{await clipboard.ClearAsync();Directory.Delete(folder,true);}
 }
}
