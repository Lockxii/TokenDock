using System.Runtime.InteropServices;
using System.Windows;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
namespace TokenDock;

sealed class ClipboardModule:IDisposable {
 [DllImport("user32.dll",SetLastError=true)]static extern bool AddClipboardFormatListener(IntPtr hwnd);
 [DllImport("user32.dll")]static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
 [DllImport("user32.dll")]static extern uint GetClipboardSequenceNumber();
 [DllImport("user32.dll")]static extern bool OpenClipboard(IntPtr hwnd);
 [DllImport("user32.dll")]static extern bool CloseClipboard();
 [DllImport("user32.dll")]static extern bool IsClipboardFormatAvailable(uint format);
 [DllImport("user32.dll")]static extern IntPtr GetClipboardData(uint format);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterClipboardFormat(string name);
 [DllImport("kernel32.dll")]static extern UIntPtr GlobalSize(IntPtr handle);
 [DllImport("kernel32.dll")]static extern IntPtr GlobalLock(IntPtr handle);
 [DllImport("kernel32.dll")]static extern bool GlobalUnlock(IntPtr handle);
 readonly DispatcherTimer retry=new(){Interval=TimeSpan.FromMilliseconds(120)};HwndSource? source;IntPtr hwnd;int attempts;uint exclude,allow,png,ownSequence;bool active,writing;
 public readonly CopyHistory History=new();public event Action? Changed;public string Status {get;private set;}="Désactivé";
 public ClipboardModule(){retry.Tick+=(_,_)=>{retry.Stop();Read();};}
 public void Enable(IntPtr window,bool value){
  if(active==value)return;
  if(!value){active=false;retry.Stop();if(hwnd!=IntPtr.Zero)RemoveClipboardFormatListener(hwnd);source?.RemoveHook(Hook);source=null;History.Clear();Status="Désactivé";Changed?.Invoke();return;}
  hwnd=window;source=HwndSource.FromHwnd(hwnd);if(source==null||!AddClipboardFormatListener(hwnd)){Status="Presse-papiers indisponible";Changed?.Invoke();return;}
  exclude=RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");allow=RegisterClipboardFormat("CanIncludeInClipboardHistory");png=RegisterClipboardFormat("PNG");source.AddHook(Hook);active=true;Status="Activé · texte, images et fichiers";Changed?.Invoke();
 }
 IntPtr Hook(IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled){if(active&&message==0x031D){if(writing||GetClipboardSequenceNumber()==ownSequence)return IntPtr.Zero;attempts=0;retry.Stop();Read();}return IntPtr.Zero;}
 void Read(){
  if(!active)return;if(!OpenClipboard(hwnd)){if(++attempts<4)retry.Start();return;}
  try{
   if(IsClipboardFormatAvailable(exclude))return;
   if(IsClipboardFormatAvailable(allow)){var h=GetClipboardData(allow);if(h!=IntPtr.Zero&&GlobalSize(h).ToUInt64()>=4){var p=GlobalLock(h);if(p!=IntPtr.Zero)try{if(Marshal.ReadInt32(p)==0)return;}finally{GlobalUnlock(h);}}}
   CopyItem? item=null;
   if(IsClipboardFormatAvailable(15))item=ReadFiles();
   else if(IsClipboardFormatAvailable(png)||IsClipboardFormatAvailable(2))item=ReadImage();
   else if(IsClipboardFormatAvailable(13)){
    var handle=GetClipboardData(13);ulong bytes=GlobalSize(handle).ToUInt64();if(bytes==0||bytes>65538)return;var data=GlobalLock(handle);if(data==IntPtr.Zero)return;
    try{string text=Marshal.PtrToStringUni(data,(int)bytes/2)??"";int end=text.IndexOf('\0');item=CopyItem.FromText(end<0?text:text[..end]);}finally{GlobalUnlock(handle);}
   }
   History.Add(item);
  }catch(Exception error)when(error is ExternalException or ArgumentException or InvalidOperationException or NotSupportedException or IOException){Status="Cette copie n’a pas pu être ajoutée.";}
  finally{CloseClipboard();}Changed?.Invoke();
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern uint DragQueryFile(IntPtr drop,uint index,StringBuilder? path,uint length);
 [DllImport("gdi32.dll",EntryPoint="GetObjectW")]static extern int GetBitmapObject(IntPtr handle,int size,out NativeBitmap bitmap);
 [StructLayout(LayoutKind.Sequential)]struct NativeBitmap{public int Type,Width,Height,WidthBytes;public ushort Planes,BitsPixel;public IntPtr Bits;}
 CopyItem? ReadFiles(){
  var handle=GetClipboardData(15);uint count=DragQueryFile(handle,uint.MaxValue,null,0);if(count==0||count>100)return null;var files=new List<string>();long total=0;
  for(uint i=0;i<count;i++){uint length=DragQueryFile(handle,i,null,0);if(length==0||length>32767||(total+=length)>32768)return null;var path=new StringBuilder((int)length+1);if(DragQueryFile(handle,i,path,length+1)!=length)return null;files.Add(path.ToString());}
  return CopyItem.FromFiles(files.ToArray());
 }
 CopyItem? ReadImage(){
  if(IsClipboardFormatAvailable(png)){
   var handle=GetClipboardData(png);ulong size=GlobalSize(handle).ToUInt64();
   if(size>0&&size<=CopyItem.MaxImageBytes){var pointer=GlobalLock(handle);if(pointer!=IntPtr.Zero)try{byte[] bytes=new byte[(int)size];Marshal.Copy(pointer,bytes,0,bytes.Length);var item=CopyItem.FromPng(bytes);if(item!=null)return item;}catch(Exception error)when(error is ArgumentException or NotSupportedException or IOException or InvalidOperationException){}finally{GlobalUnlock(handle);}}
  }
  if(!IsClipboardFormatAvailable(2))return null;var bitmap=GetClipboardData(2);
  if(GetBitmapObject(bitmap,Marshal.SizeOf<NativeBitmap>(),out var info)==0||!CopyItem.ValidImageSize(info.Width,info.Height))return null;
  // The clipboard owns this HBITMAP. Snapshot it while locked; never delete its handle.
  return CopyItem.FromImage(Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()));
 }

 public void Copy(CopyItem item){retry.Stop();writing=true;try{item.Write();ownSequence=GetClipboardSequenceNumber();}finally{writing=false;}}
 readonly FileShelf shelf=new();
 public async Task<string?> StoreFiles(string[] paths){try{var item=await shelf.Store(paths);History.Add(item);Changed?.Invoke();return null;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){return e.Message;}}
 public void AddPhoto(string path,BitmapSource image){var item=CopyItem.FromImage(image);if(item==null)return;History.Add(item with{Files=[path]});Changed?.Invoke();}
 public void Remove(CopyItem item){History.Remove(item.Key);Changed?.Invoke();}
 public void Clear(){History.Clear();Changed?.Invoke();}
 public void Dispose(){Enable(hwnd,false);retry.Stop();shelf.Dispose();}
}
