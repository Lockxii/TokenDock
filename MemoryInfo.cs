using System.Runtime.InteropServices;
namespace TokenDock;
readonly record struct MemoryInfo(ulong Total,ulong Available) {
 public double FreeRatio=>Total==0?1:Math.Clamp((double)Available/Total,0,1);
 public double CyclesPerSecond=>.45+5*Math.Pow(1-FreeRatio,2);
 public string AvailableText=>$"{Available/1073741824d:0.0} GB available / {Total/1073741824d:0.0} GB";
 public static MemoryInfo Read(){
  if(OperatingSystem.IsLinux()){
   try{return ParseLinux(File.ReadLines("/proc/meminfo"));}catch(IOException){return default;}catch(UnauthorizedAccessException){return default;}
  }
  if(OperatingSystem.IsMacOS())return ReadMac();
  if(!OperatingSystem.IsWindows())return default;
  var s=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};return GlobalMemoryStatusEx(ref s)?new(s.Total,s.Available):default;
 }
 internal static MemoryInfo ParseLinux(IEnumerable<string> lines){
  ulong total=0,available=0;bool hasAvailable=false;
  foreach(var line in lines){
   var fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   if(fields.Length!=3||fields[2]!="kB"||!ulong.TryParse(fields[1],out var kb)||kb>ulong.MaxValue/1024)continue;
   if(fields[0]=="MemTotal:")total=kb*1024;
   if(fields[0]=="MemAvailable:"){available=kb*1024;hasAvailable=true;}
  }
  // MemFree excludes reclaimable caches and would falsely report memory pressure.
  return total>0&&hasAvailable?new(total,Math.Min(total,available)):default;
 }
 const string LibSystem="/usr/lib/libSystem.B.dylib";
 static readonly Lazy<uint> Host=new(mach_host_self);
 static MemoryInfo ReadMac(){
  nuint size=8;ulong total=0;
  if(sysctlbyname("hw.memsize",ref total,ref size,IntPtr.Zero,0)!=0||size!=8)return default;
  uint host=Host.Value,count=15;
  var pages=new uint[15];
  if(host_page_size(host,out uint pageSize)!=0||host_statistics(host,2,pages,ref count)!=0||count<3)return default;
  // Conservative reclaimable estimate: free + inactive. Speculative is already
  // included in free_count; compressor pages must not be counted as available.
  return new(total,Math.Min(total,((ulong)pages[0]+pages[2])*pageSize));
 }
 [DllImport(LibSystem)]static extern int sysctlbyname([MarshalAs(UnmanagedType.LPUTF8Str)]string name,ref ulong value,ref nuint size,IntPtr newValue,nuint newSize);
 [DllImport(LibSystem)]static extern uint mach_host_self();
 [DllImport(LibSystem)]static extern int host_page_size(uint host,out uint size);
 [DllImport(LibSystem)]static extern int host_statistics(uint host,int flavor,[Out]uint[] data,ref uint count);
 [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,Load;public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended;}
 [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GlobalMemoryStatusEx(ref MemoryStatus s);
}
