using System.Text.Json;
namespace TokenDock;

record CustomSource(string Id,string Name,string Path);
sealed class Preferences {
 public string[] Visible {get;set;}=["claude","codex","opencode"];
 public bool CounterAbove {get;set;}
 public string Counter {get;set;}="all";
 public string[]? LayoutOrder {get;set;}
 public double CatOrange {get;set;}=40;
 public double CatRed {get;set;}=20;
 public double CatSpeed {get;set;}=1;
 public bool CatVisible {get;set;}=true;
 public string[] Layout=> (LayoutOrder??Visible.Concat(ModuleOrder).Append("cat").ToArray()).Concat(Ids).Concat(new[]{"music","clipboard","camera","cat"}).Distinct().Where(IsShown).ToArray();
 public string[] ModuleOrder {get;set;}=["music","clipboard"];
 public bool MusicPreview {get;set;}=true;
 public bool MusicTitle {get;set;}=true;
 public bool Camera {get;set;}
 public bool Music {get;set;}
 public bool ClipboardHistory {get;set;}
 public CustomSource[] Sources {get;set;}=[];
 public static readonly string[] Builtin=["codex","claude","opencode"];
 public string[] Ids=>Builtin.Concat(Sources.Select(s=>s.Id)).ToArray();
 public string Name(string id)=>id switch{"all"=>"All tools","codex"=>"Codex","claude"=>"Claude Code","opencode"=>"OpenCode",_=>Sources.FirstOrDefault(s=>s.Id==id)?.Name??id};
 public Preferences Copy()=>JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(this))!;
 public bool IsShown(string id)=>id switch{"music"=>Music,"clipboard"=>ClipboardHistory||Camera,"camera"=>Camera,"cat"=>CatVisible,_=>Visible.Contains(id)};
 public void ShowItem(string id,bool show){LayoutOrder??=Layout;if(id=="music")Music=show;else if(id=="clipboard")ClipboardHistory=show;else if(id=="camera")Camera=show;else if(id=="cat")CatVisible=show;else if(Ids.Contains(id))Visible=show?Visible.Append(id).Distinct().ToArray():Visible.Where(x=>x!=id).ToArray();}
 public void MoveItem(string id,string? target,bool after=false){
  bool Valid(string value)=>Ids.Contains(value)||value is "music" or "clipboard" or "camera" or "cat";
  if(!Valid(id)||target!=null&&!Valid(target)||id==target)return;
  var list=Layout.Where(x=>x!=id).ToList();ShowItem(id,true);int at=target==null?list.Count:list.IndexOf(target);if(at<0)at=list.Count;else if(after)at++;list.Insert(Math.Min(at,list.Count),id);LayoutOrder=list.ToArray();Visible=list.Where(Ids.Contains).ToArray();ModuleOrder=list.Where(x=>x is "music" or "clipboard").Concat(ModuleOrder).Distinct().ToArray();

 }
 public void Normalize(){
  CatOrange=double.IsFinite(CatOrange)?Math.Clamp(CatOrange,2,100):40;CatRed=double.IsFinite(CatRed)?Math.Clamp(CatRed,1,CatOrange-1):Math.Min(20,CatOrange-1);CatSpeed=double.IsFinite(CatSpeed)?Math.Clamp(CatSpeed,.25,2):1;
  if(Camera&&LayoutOrder==null)LayoutOrder=Layout;
  ModuleOrder=(ModuleOrder??[]).Where(id=>id is "music" or "clipboard").Concat(new[]{"music","clipboard"}).Distinct().ToArray();
  Sources=(Sources??[]).Where(s=>s!=null&&!string.IsNullOrWhiteSpace(s.Id)&&!Builtin.Contains(s.Id)&&s.Id is not("all" or "music" or "clipboard" or "camera" or "cat")&&!string.IsNullOrWhiteSpace(s.Name)&&!string.IsNullOrWhiteSpace(s.Path)&&System.IO.Path.IsPathFullyQualified(s.Path)).DistinctBy(s=>s.Id).Take(3).Select(s=>s with{Name=s.Name.Trim()[..Math.Min(32,s.Name.Trim().Length)]}).ToArray();
  if(LayoutOrder!=null)LayoutOrder=LayoutOrder.Where(id=>Ids.Contains(id)||id is "music" or "clipboard" or "camera" or "cat").Distinct().ToArray();
  Visible=(Visible??[]).Where(Ids.Contains).Distinct().ToArray();if(!Ids.Contains(Counter)&&Counter!="all")Counter="all";
 }
 static string FilePath=>System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TokenDock","personalization.json");
 public static Preferences Load(){try{var p=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath))??new();p.Normalize();return p;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){return new();}}
 public void Save(){Normalize();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);string temporary=FilePath+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(temporary,FilePath,true);}
}

// Optional adapters consume explicit local cumulative usage snapshots; they never guess another application's format.
sealed class CustomFeeds:IDisposable {
 sealed class Feed:IDisposable {
  public readonly CustomSource Source;public readonly Meter Meter;public Usage? Baseline;public FileSystemWatcher? Watcher;
  readonly Timer timer;readonly object gate;readonly Action changed;bool disposed,scheduled;
  public Feed(CustomSource source,object gate,Action changed){Source=source;Meter=new(source.Name);this.gate=gate;this.changed=changed;timer=new(_=>Read(),null,Timeout.Infinite,Timeout.Infinite);
   try{Read();Watcher=new FileSystemWatcher(System.IO.Path.GetDirectoryName(source.Path)!,System.IO.Path.GetFileName(source.Path)){NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.Size|NotifyFilters.FileName};Watcher.Changed+=(_,_)=>Queue();Watcher.Created+=(_,_)=>Queue();Watcher.Renamed+=(_,_)=>Queue();Watcher.Deleted+=(_,_)=>Queue();Watcher.Error+=(_,_)=>Queue();Watcher.EnableRaisingEvents=true;Read();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){Meter.Status="Cannot access source";}
  }
  void Queue(){lock(gate)if(!disposed&&!scheduled){scheduled=true;timer.Change(60,Timeout.Infinite);}}
  void Read(){lock(gate){scheduled=false;if(disposed)return;try{
    using var f=new FileStream(Source.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(f.Length>65536)throw new IOException();using var doc=JsonDocument.Parse(f);var j=doc.RootElement;
    if(j.ValueKind!=JsonValueKind.Object||!j.TryGetProperty("input",out var input)||!j.TryGetProperty("output",out var output)||input.ValueKind!=JsonValueKind.Number||output.ValueKind!=JsonValueKind.Number||!input.TryGetInt64(out long a)||!output.TryGetInt64(out long b)||a<0||b<0)throw new JsonException();
    long c=0;if(j.TryGetProperty("cache",out var cache)&&(cache.ValueKind!=JsonValueKind.Number||!cache.TryGetInt64(out c)||c<0))throw new JsonException();checked{_ =a+b+c;}
    var value=new Usage(a,b,c);if(Baseline is { } old){var delta=value.Delta(old);checked{var next=new Usage(Meter.Value.Input+delta.Input,Meter.Value.Output+delta.Output,Meter.Value.Cache+delta.Cache);_ = next.Input+next.Output+next.Cache;Meter.Value=next;}if(delta.Total>0)Meter.Updated=DateTime.Now;}Baseline=value;Meter.Status="Connected · custom source";
   }catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or OverflowException){Meter.Status="Source unavailable or invalid JSON";}}changed();}
  public void Dispose(){lock(gate){disposed=true;Watcher?.Dispose();timer.Dispose();}}
 }
 readonly object gate=new();Feed[] feeds=[];public event Action? Changed;
 public void Configure(CustomSource[] sources){lock(gate){if(feeds.Select(f=>f.Source).SequenceEqual(sources))return;var old=feeds;feeds=sources.Select(s=>old.FirstOrDefault(f=>f.Source==s)??new Feed(s,gate,()=>Changed?.Invoke())).ToArray();foreach(var f in old.Except(feeds))f.Dispose();}}
 public Meter[] Snapshot(){lock(gate)return feeds.Select(f=>new Meter(f.Meter.Name){Value=f.Meter.Value,Updated=f.Meter.Updated,Status=f.Meter.Status}).ToArray();}
 public void Reset(){lock(gate)foreach(var f in feeds){f.Meter.Value=new();f.Meter.Updated=null;}}
 public void Dispose(){lock(gate){foreach(var f in feeds)f.Dispose();feeds=[];}}
}
