using System.Runtime.InteropServices;
namespace TokenDock;
readonly record struct MemoryInfo(ulong Total,ulong Available) {
 public double FreeRatio=>Total==0?1:Math.Clamp((double)Available/Total,0,1);
 public double CyclesPerSecond=>.45+5*Math.Pow(1-FreeRatio,2);
 public string AvailableText=>$"{Available/1073741824d:0.0} Go disponibles / {Total/1073741824d:0.0} Go";
 public static MemoryInfo Read(){var s=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};return GlobalMemoryStatusEx(ref s)?new(s.Total,s.Available):default;}
 [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,Load;public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended;}
 [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GlobalMemoryStatusEx(ref MemoryStatus s);
}
