using Avalonia.Threading;
namespace TokenDock;
static class MediaChecks {
 public static int Run(){
  bool mac=OperatingSystem.IsMacOS();
  if(mac&&(Environment.GetEnvironmentVariable("CI")!="true"||Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated"))throw new InvalidOperationException("Use an isolated macOS CI session.");
  using var music=new Music();
  // Headless dispatcher timers are not a native event loop. Explicitly pump polling.
  void Until(Func<bool> done){var watch=System.Diagnostics.Stopwatch.StartNew();long refreshAt=0;while(!done()&&watch.ElapsedMilliseconds<10000){Dispatcher.UIThread.RunJobs();if(watch.ElapsedMilliseconds>=refreshAt){_=music.Refresh();refreshAt=watch.ElapsedMilliseconds+250;}Thread.Sleep(10);}if(!done())throw new InvalidOperationException("Media fixture timed out: "+music.Status+"; state="+music.Track+"; position="+music.Position);}
  void Command(string name,double value=0){var task=music.Command(name,value);Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
  music.Enable(true);Until(()=>music.Track.Player==(mac?"app.tokendock.mediafixture":"TokenDockFixture"));
  if(music.Track.Title!="Synthetic track"||music.Track.Length!=120||!music.Track.Playing)throw new InvalidOperationException("Media metadata mismatch.");
  if(mac)Until(()=>music.Artwork!=null);
  Command("play-pause");Until(()=>!music.Track.Playing);
  Command("position",40);Until(()=>Math.Abs(music.Position-40)<1);
  Command("next");Until(()=>music.Position<1&&(!mac||music.Track.Title=="Synthetic next"));
  Command("previous");if(mac)Until(()=>music.Track.Title=="Synthetic track");music.Enable(false);
  if(music.Track!=Track.Empty)throw new InvalidOperationException("Disabled music retained state.");
  Console.WriteLine("PASS: Native media metadata, artwork (macOS), pause, seeking, next/previous and disable against isolated synthetic player.");return 0;
 }
}
