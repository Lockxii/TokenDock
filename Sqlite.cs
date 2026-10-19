using System.Runtime.InteropServices;
namespace TokenDock;
sealed class Sqlite:IDisposable {
 static Sqlite()=>NativeLibrary.SetDllImportResolver(typeof(Sqlite).Assembly,(name,assembly,path)=>name=="winsqlite3"?NativeLibrary.Load(OperatingSystem.IsWindows()?"winsqlite3.dll":OperatingSystem.IsMacOS()?"/usr/lib/libsqlite3.dylib":"libsqlite3.so.0",assembly,path):IntPtr.Zero);
 IntPtr db;
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out IntPtr db,int flags,IntPtr vfs);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,int n,out IntPtr stmt,IntPtr tail);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr s);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_count(IntPtr s);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr s,int i);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr s);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr d);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_busy_timeout(IntPtr d,int ms);
 public Sqlite(string path){if(sqlite3_open_v2(path,out db,1,IntPtr.Zero)!=0){Dispose();throw new IOException("Database unavailable");}sqlite3_busy_timeout(db,100);}
 public IEnumerable<string[]> Rows(string query){if(sqlite3_prepare_v2(db,query,-1,out var s,IntPtr.Zero)!=0)throw new IOException("Incompatible SQLite schema");try{int rc;while((rc=sqlite3_step(s))==100){var r=new string[sqlite3_column_count(s)];for(int i=0;i<r.Length;i++)r[i]=Marshal.PtrToStringUTF8(sqlite3_column_text(s,i))??"0";yield return r;}if(rc!=101)throw new IOException("SQLite busy");}finally{sqlite3_finalize(s);}}
 public void Dispose(){if(db!=IntPtr.Zero){sqlite3_close(db);db=IntPtr.Zero;}}
}
