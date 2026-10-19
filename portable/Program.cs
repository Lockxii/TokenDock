using System.Security.Cryptography;
using System.Text.Json;
using TokenDock;

// Integration harness only. Never starts the desktop, watches real conversations,
// activates a camera, or reads the system clipboard.
if(args.Length!=2||args[0]!="--check"){
 Console.Error.WriteLine("Development integration harness: --check <synthetic SQLite fixture>");
 return 2;
}
int checks=0;
void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);checks++;}
var root=Path.Combine(Path.GetTempPath(),"tokendock-port-check-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try{
 var memory=MemoryInfo.Read();
 Check(memory.Total>0&&memory.Available<=memory.Total,"Native memory API");
 Check(memory.FreeRatio is >=0 and <=1,"Native memory bounds");
 Check(MemoryInfo.ParseLinux(["MemTotal: 1000 kB","MemFree: 10 kB","MemAvailable: 700 kB"])==new MemoryInfo(1024000,716800),"Linux reclaimable memory");
 Check(MemoryInfo.ParseLinux(["MemTotal: 1000 kB","MemFree: 10 kB"])==default,"Missing MemAvailable must not become false pressure");
 Check(MemoryInfo.ParseLinux(["MemTotal: 1000 kB","MemAvailable: 2000 kB"]).Available==1024000,"Clamp inconsistent memory samples");
 Check(MemoryInfo.ParseLinux(["MemTotal: 18446744073709551615 kB","MemAvailable: -1 kB"])==default,"Malformed memory input");
 Check(MemoryInfo.ParseLinux(["MemTotal: 1000 MB","MemAvailable: 700 kB"])==default,"Validate memory units");
 var fixture=Path.GetFullPath(args[1]);
 var before=SHA256.HashData(File.ReadAllBytes(fixture));
 using(var db=new Sqlite(fixture)){
  Check(db.Rows("SELECT name FROM fixture ORDER BY id").Select(r=>r[0]).SequenceEqual(new[]{"Synthetic α","Synthetic β"}),"Native SQLite UTF-8");
  bool refused=false;
  try{_ = db.Rows("INSERT INTO fixture VALUES (3,'must not write')").ToArray();}catch(IOException){refused=true;}
  Check(refused,"SQLite must be read-only");
 }
 Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(fixture))),"SQLite fixture unchanged");
 string absent=Path.Combine(root,"absent.db");bool missing=false;
 try{using var db=new Sqlite(absent);}catch(IOException){missing=true;}
 Check(missing&&!File.Exists(absent),"SQLite must not create missing source");
 using(var monitor=new TokenDock.Monitor()){
  string log=Path.Combine(root,"fixture.jsonl");
  string Record(int input,int output)=>JsonSerializer.Serialize(new{timestamp=DateTimeOffset.UtcNow,payload=new{type="token_count",info=new{total_token_usage=new{input_tokens=input,output_tokens=output,cached_input_tokens=10}}}});
  File.WriteAllText(log,Record(100,20)+"\n");monitor.ReadTail(log,0);
  Check(monitor.Snapshot()[0].Value==new Usage(90,20,10),"Codex incremental totals");
  monitor.ReadTail(log,0);
  Check(monitor.Snapshot()[0].Value.Total==120,"No duplicate reads");
  var partial=Record(150,30);File.AppendAllText(log,partial[..20]);monitor.ReadTail(log,0);
  Check(monitor.Snapshot()[0].Value.Total==120,"Incomplete record withheld");
  File.AppendAllText(log,partial[20..]+"\n");monitor.ReadTail(log,0);
  Check(monitor.Snapshot()[0].Value==new Usage(140,30,10),"Partial record completed");
  if(OperatingSystem.IsLinux()){
   var upper=Path.Combine(root,"Fixture.jsonl");File.WriteAllText(upper,Record(40,1)+"\n");monitor.ReadTail(upper,0);
   Check(monitor.Snapshot()[0].Value.Total==221,"Case-sensitive source paths");
  }
 }
 var prefs=new Preferences{Camera=true,Music=true,ClipboardHistory=false};prefs.Normalize();
 prefs.MoveItem("camera","codex");prefs.MoveItem("cat","camera");
 Check(Array.IndexOf(prefs.Layout,"cat")+1==Array.IndexOf(prefs.Layout,"camera")&&Array.IndexOf(prefs.Layout,"camera")+1==Array.IndexOf(prefs.Layout,"codex"),"Unified ordering across module kinds");
 Check(prefs.IsShown("clipboard")&&!prefs.ClipboardHistory,"Camera exposes Clipboard without enabling capture");
 var copy=prefs.Copy();copy.CatRed=double.NaN;copy.CatSpeed=100;copy.Normalize();
 Check(copy.CatSpeed==2&&double.IsFinite(copy.CatRed),"Preference bounds on every OS");
 Check(prefs.CatSpeed==1,"Cancelled draft does not mutate original");
 Console.WriteLine($"PASS: {checks} portable checks on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
 return 0;
}finally{Directory.Delete(root,true);}
