using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace TokenDock;
record Usage(long Input=0,long Output=0,long Cache=0) {
 public long Total=>Input+Output+Cache;
 public static Usage operator +(Usage a, Usage b)=>new(a.Input+b.Input,a.Output+b.Output,a.Cache+b.Cache);
 public Usage Delta(Usage old)=>new(Math.Max(0,Input-old.Input),Math.Max(0,Output-old.Output),Math.Max(0,Cache-old.Cache));
}
class Meter(string name) {
 public string Name=name, Status="Waiting"; public Usage Value=new(); public DateTime? Updated;
}
static class Parse {
 public static JsonElement Get(JsonElement j,string k)=>j.ValueKind==JsonValueKind.Object && j.TryGetProperty(k,out var v)?v:default;
 public static long N(JsonElement j,string k)=>Get(j,k).TryNumber();
 static long TryNumber(this JsonElement j)=>j.ValueKind==JsonValueKind.Number && j.TryGetInt64(out var n)?Math.Max(0,n):0;
 public static string S(JsonElement j,string k)=>Get(j,k).ValueKind==JsonValueKind.String?Get(j,k).GetString()??"":"";
 public static Usage Codex(JsonElement u)=>new(Math.Max(0,N(u,"input_tokens")-N(u,"cached_input_tokens")),N(u,"output_tokens"),N(u,"cached_input_tokens")+N(u,"cache_write_input_tokens"));
 public static Usage Claude(JsonElement u)=>new(N(u,"input_tokens"),N(u,"output_tokens"),N(u,"cache_read_input_tokens")+N(u,"cache_creation_input_tokens"));
}
class Tail { public long Offset; public List<byte> Pending=[]; public Usage Last=new(); public bool Skip; }
sealed class Monitor : IDisposable {
 public readonly Meter[] Meters=[new("Codex"),new("Claude Code"),new("OpenCode")];
 readonly DateTime started=DateTime.UtcNow;
 readonly ConcurrentDictionary<string,int> dirty=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Tail> tails=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Usage> claude=new(); readonly Dictionary<string,Usage> open=new();
 readonly List<FileSystemWatcher> watchers=[];
 readonly CancellationTokenSource stop=new(); readonly object gate=new();
 readonly string[] roots; readonly string db;
 public event Action? Changed;
 readonly SemaphoreSlim wake=new(0,1);Task? loop; long cursor; string? table; string dbStamp="";
 void Wake(){try{wake.Release();}catch(SemaphoreFullException){}}
 void Dirty(string path,int i){dirty[path]=i;Wake();}
 public Monitor() {
  var home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  roots=[Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME")??Path.Combine(home,".codex"),"sessions"),Path.Combine(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")??Path.Combine(home,".claude"),"projects")];
  db=Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME")??Path.Combine(home,".local","share"),"opencode","opencode.db");
 }
 public Meter[] Snapshot(){lock(gate)return Meters.Select(m=>new Meter(m.Name){Value=m.Value,Status=m.Status,Updated=m.Updated}).ToArray();}
 void Status(int i,string s){bool changed;lock(gate){changed=Meters[i].Status!=s;Meters[i].Status=s;}if(changed)Changed?.Invoke();}
 void Add(int i,Usage u){if(u.Total==0)return;lock(gate){Meters[i].Value+=u;Meters[i].Updated=DateTime.Now;}Changed?.Invoke();}
 public void Reset(){lock(gate)foreach(var m in Meters){m.Value=new();m.Updated=null;}}
 public void Start(){loop=Task.Run(async()=>{
  try{Setup(); ReadOpen(true);}catch(Exception e){Status(2,"Error: "+e.Message);}
  var nextDb=DateTime.UtcNow;var nextScan=DateTime.UtcNow.AddSeconds(30);
  while(!stop.IsCancellationRequested){
   foreach(var p in dirty.Keys.ToArray())if(dirty.TryRemove(p,out int i))try{ReadTail(p,i);Status(i,"Connected · local logs");}catch(Exception e){Status(i,"Read error: "+e.Message);}
   if(DateTime.UtcNow>=nextDb){try{ReadOpen(false);}catch(Exception e){Status(2,"Read error: "+e.Message);}nextDb=DateTime.UtcNow.AddSeconds(1);}
   if(DateTime.UtcNow>=nextScan){Setup();nextScan=DateTime.UtcNow.AddSeconds(30);}
   try{await wake.WaitAsync(TimeSpan.FromSeconds(1),stop.Token);}catch(OperationCanceledException){break;}
  }
 });}
 void Setup(){for(int i=0;i<2;i++){
  int idx=i; var root=roots[i];
  if(!Directory.Exists(root)){Status(i,"Source not found");continue;}
  if(!watchers.Any(w=>w.Path==root)){
   var w=new FileSystemWatcher(root,"*.jsonl"){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|NotifyFilters.Size,InternalBufferSize=32768};
   w.Changed+=(_,e)=>Dirty(e.FullPath,idx);w.Created+=(_,e)=>Dirty(e.FullPath,idx);w.Renamed+=(_,e)=>Dirty(e.FullPath,idx);
   w.Error+=(_,_)=>{Status(idx,"Resyncing");};w.EnableRaisingEvents=true;watchers.Add(w);Status(i,"Connected · waiting");
  }
  try{foreach(var f in Directory.EnumerateFiles(root,"*.jsonl",new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true}))if(File.GetLastWriteTimeUtc(f)>=started)dirty[f]=i;}catch(IOException){Status(i,"Source temporarily inaccessible");}
 }}
 internal void ReadTail(string path,int index){
  if(!File.Exists(path))return;
  if(!tails.TryGetValue(path,out var t))tails[path]=t=new();
  using var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,65536,FileOptions.SequentialScan);
  if(f.Length<t.Offset){t.Offset=0;t.Pending.Clear();t.Skip=false;}
  f.Position=t.Offset;byte[] buf=new byte[65536];int n;
  while(!stop.IsCancellationRequested&&(n=f.Read(buf))>0){for(int k=0;k<n;k++){
   byte b=buf[k];if(b==10){if(!t.Skip&&t.Pending.Count>0)Consume(Encoding.UTF8.GetString(t.Pending.ToArray()),index,t);t.Pending.Clear();t.Skip=false;}
   else if(!t.Skip){t.Pending.Add(b);if(t.Pending.Count>16*1024*1024){t.Pending.Clear();t.Skip=true;Status(index,"A line over 16 MB was skipped");}}
  }t.Offset=f.Position;}
 }
 void Consume(string line,int i,Tail t){
  // Text of conversations is discarded; only usage fields are retained.
  if(i==0&&!line.Contains("token_count",StringComparison.Ordinal))return;
  if(i==1&&!line.Contains("\"usage\"",StringComparison.Ordinal))return;
  try{using var d=JsonDocument.Parse(line);var j=d.RootElement;
   bool fresh=DateTimeOffset.TryParse(Parse.S(j,"timestamp"),out var when)&&when.UtcDateTime>=started;
   if(i==0){var p=Parse.Get(j,"payload");if(Parse.S(p,"type")!="token_count")return;var u=Parse.Get(Parse.Get(p,"info"),"total_token_usage");if(u.ValueKind!=JsonValueKind.Object)return;var v=Parse.Codex(u);if(fresh)Add(i,v.Delta(t.Last));t.Last=new(Math.Max(v.Input,t.Last.Input),Math.Max(v.Output,t.Last.Output),Math.Max(v.Cache,t.Last.Cache));}
   else{var msg=Parse.Get(j,"message");var u=Parse.Get(msg,"usage");string id=Parse.S(msg,"id");if(id==""||u.ValueKind!=JsonValueKind.Object)return;var v=Parse.Claude(u);claude.TryGetValue(id,out var old);if(fresh)Add(i,v.Delta(old??new()));claude[id]=new(Math.Max(v.Input,old?.Input??0),Math.Max(v.Output,old?.Output??0),Math.Max(v.Cache,old?.Cache??0));}
  }catch(JsonException){/* Incomplete JSON lines are not usage events. */}
 }
 void ReadOpen(bool initial){
  if(!File.Exists(db)){Status(2,"Source not found");return;}
  var stamp=$"{File.GetLastWriteTimeUtc(db).Ticks}:{File.GetLastWriteTimeUtc(db+"-wal").Ticks}:{(File.Exists(db+"-wal")?new FileInfo(db+"-wal").Length:0)}";
  if(!initial && stamp==dbStamp)return;
  using var sql=new Sqlite(db);
  if(table==null){var names=sql.Rows("SELECT name FROM sqlite_master WHERE type='table'").Select(r=>r[0]).ToArray();table=names.Contains("session_v2")?"session_v2":names.Contains("message")?"message":null;if(table==null){Status(2,"Unknown OpenCode format");return;}initial=true;}
  var next=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
  string select=table=="session_v2"?"SELECT id,tokens_input,tokens_output,tokens_cache_read+tokens_cache_write FROM session_v2":"SELECT id,coalesce(json_extract(data,'$.tokens.input'),0),coalesce(json_extract(data,'$.tokens.output'),0)+coalesce(json_extract(data,'$.tokens.reasoning'),0),coalesce(json_extract(data,'$.tokens.cache.read'),0)+coalesce(json_extract(data,'$.tokens.cache.write'),0) FROM message";
  // Baseline once. Afterwards only records modified since the previous successful query.
  foreach(var r in sql.Rows(select+(initial?"":" WHERE time_updated >= "+Math.Max(0,cursor-2000)))){
   var v=new Usage(long.Parse(r[1]),long.Parse(r[2]),long.Parse(r[3]));open.TryGetValue(r[0],out var old);if(!initial)Add(2,v.Delta(old??new()));open[r[0]]=v;
  }
  dbStamp=stamp;cursor=next;Status(2,"Connected · read-only SQLite");
 }
 public void Dispose(){stop.Cancel();foreach(var w in watchers)w.Dispose();}
}
sealed class Sqlite:IDisposable {
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
