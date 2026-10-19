using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace TokenDock;

// Working sets describe resident pages, including pages shared by several processes.
internal record MemoryConsumer(string Name,long Bytes,int Count,ImageSource? Icon);
internal sealed class MemoryConsumers {
 readonly Dictionary<string,ImageSource?> icons=new(StringComparer.OrdinalIgnoreCase);
 public MemoryConsumer[] Read(){
  var rows=new List<(string Name,int Id,long Bytes)>();
  foreach(var process in Process.GetProcesses())using(process){
   try{if(process.Id!=0)rows.Add((process.ProcessName,process.Id,process.WorkingSet64));}
   catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}
  }
  return rows.GroupBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).Select(g=>new {Name=g.Key,Bytes=g.Sum(p=>p.Bytes),Count=g.Count(),Ids=g.OrderByDescending(p=>p.Bytes).Select(p=>p.Id).ToArray()})
   .OrderByDescending(g=>g.Bytes).Take(3).Select(g=>new MemoryConsumer(g.Name,g.Bytes,g.Count,IconFor(g.Name,g.Ids))).ToArray();
 }
 ImageSource? IconFor(string name,int[] ids){
  if(icons.TryGetValue(name,out var cached))return cached;
  ImageSource? result=null;
  foreach(int id in ids.Take(3))try{
   using var p=Process.GetProcessById(id);var path=p.MainModule?.FileName;if(path==null)continue;
   using var icon=System.Drawing.Icon.ExtractAssociatedIcon(path);if(icon==null)continue;
   var bitmap=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(20,20));bitmap.Freeze();result=bitmap;break;
  }catch(Exception e)when(e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException){}
  if(icons.Count>=32)icons.Clear();icons[name]=result;return result;
 }
}
