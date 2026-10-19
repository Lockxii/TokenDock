using Avalonia.Threading;
namespace TokenDock;
static class MediaChecks {
 public static int Run(){
  if(!OperatingSystem.IsLinux())throw new InvalidOperationException("Use the isolated Linux MPRIS fixture.");
  using var music=new Music();
  void Until(Func<bool> done){var watch=System.Diagnostics.Stopwatch.StartNew();while(!done()&&watch.ElapsedMilliseconds<10000){Dispatcher.UIThread.RunJobs();Thread.Sleep(10);}if(!done())throw new InvalidOperationException("MPRIS fixture timed out: "+music.Status);}
  void Command(string name,double value=0){var task=music.Command(name,value);Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
  music.Enable(true);Until(()=>music.Track.Player=="TokenDockFixture");
  if(music.Track.Title!="Synthetic track"||music.Track.Length!=120||!music.Track.Playing)throw new InvalidOperationException("MPRIS metadata mismatch.");
  Command("play-pause");Until(()=>!music.Track.Playing);
  Command("position",40);Until(()=>Math.Abs(music.Position-40)<1);
  Command("next");Until(()=>music.Position<1);
  Command("previous");music.Enable(false);
  if(music.Track!=Track.Empty)throw new InvalidOperationException("Disabled music retained state.");
  Console.WriteLine("PASS: MPRIS metadata, pause, seeking, next/previous and disable against isolated D-Bus player.");return 0;
 }
}
