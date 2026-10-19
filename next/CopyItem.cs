using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace TokenDock;

enum CopyKind { Text, Image, Files }
sealed record CopyItem(CopyKind Kind,string Key,string Text,BitmapSource? Image,string[] Files,long Bytes) {
 public const long MaxImageBytes=16*1024*1024;
 public string Label=>Kind switch{CopyKind.Image=>"Image",CopyKind.Files=>Files.Length==1?"Fichier":$"{Files.Length} fichiers",_=>"Texte"};
 public string Preview=>Kind switch{CopyKind.Image=>$"{Image!.PixelWidth} × {Image.PixelHeight}",CopyKind.Files=>Path.GetFileName(Files[0].TrimEnd('\\'))+(Files.Length>1?$" +{Files.Length-1}":""),_=>Text.Replace('\r',' ').Replace('\n',' ')};
 public static CopyItem? FromText(string text)=>string.IsNullOrWhiteSpace(text)||text.Length>32768?null:new(CopyKind.Text,"t:"+text,text,null,[],text.Length*2L);
 public static CopyItem? FromFiles(string[] files){
  if(files.Length==0||files.Length>100||files.Sum(p=>(long)p.Length)>32768||files.Any(p=>p.Length==0||p.Contains('\0')||!Path.IsPathFullyQualified(p)))return null;
  var snapshot=files.ToArray();return new(CopyKind.Files,"f:"+string.Join('\0',snapshot),"",null,snapshot,snapshot.Sum(p=>p.Length*2L));
 }
 public static bool ValidImageSize(int width,int height)=>width>0&&height>0&&(long)width*height<=MaxImageBytes/4;
 public static CopyItem? FromImage(BitmapSource source){
  if(!ValidImageSize(source.PixelWidth,source.PixelHeight))return null;
  var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int stride=source.PixelWidth*4;byte[] pixels=new byte[stride*source.PixelHeight];converted.CopyPixels(pixels,stride,0);
  var image=BitmapSource.Create(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride);image.Freeze();
  return new(CopyKind.Image,$"i:{source.PixelWidth}:{source.PixelHeight}:"+Convert.ToHexString(SHA256.HashData(pixels)),"",image,[],pixels.Length);
 }
 public static CopyItem? FromPng(byte[] bytes){
  if(bytes.Length<24||bytes.Length>MaxImageBytes||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return null;
  int width=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4)),height=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4));
  if(!ValidImageSize(width,height))return null;
  using var stream=new MemoryStream(bytes);var decoder=new PngBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);return FromImage(decoder.Frames[0]);
 }
 public void Write(){
  switch(Kind){case CopyKind.Text:Clipboard.SetText(Text);break;case CopyKind.Image:if(Files.Length==0)Clipboard.SetImage(Image!);else{var data=new DataObject();data.SetData(DataFormats.Bitmap,Image!);data.SetData(DataFormats.FileDrop,Files);Clipboard.SetDataObject(data,true);}break;case CopyKind.Files:var list=new StringCollection();list.AddRange(Files);Clipboard.SetFileDropList(list);break;}
 }
}
sealed class CopyHistory {
 readonly List<CopyItem> items=[];public IReadOnlyList<CopyItem> Items=>items;
 public long Bytes=>items.Sum(i=>i.Bytes);
 public void Add(string text)=>Add(CopyItem.FromText(text));
 public void Add(CopyItem? item){if(item==null)return;items.RemoveAll(i=>i.Key==item.Key);items.Insert(0,item);while(items.Count>20||Bytes>32*1024*1024)items.RemoveAt(items.Count-1);}
 public void Remove(string key)=>items.RemoveAll(i=>i.Key==key);
 public void Clear()=>items.Clear();
}
