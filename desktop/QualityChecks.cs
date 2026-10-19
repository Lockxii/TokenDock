using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
namespace TokenDock;

static class QualityChecks {
 internal static int Accessibility(DockWindow dock){
  int count=0;
  foreach(var module in new[]{"all","music","clipboard","camera","cat","settings"}){
   dock.ShowModule(module);dock.Settle();Dispatcher.UIThread.RunJobs();
   foreach(var control in dock.GetVisualDescendants().OfType<Control>().Where(c=>c is Button or Slider or TextBox or CheckBox)){
    if(!control.IsEffectivelyVisible||control is RepeatButton)continue;
    var peer=ControlAutomationPeer.CreatePeerForElement(control);
    if(string.IsNullOrWhiteSpace(peer?.GetName()))throw new InvalidOperationException($"Unnamed accessible control in {module}: {control.GetType().Name}");
    count++;
   }
  }
  // Exercise real keyboard routing, not the reorder implementation in isolation.
  var preview=dock.GetVisualDescendants().OfType<Button>().First(b=>b.Tag is string);
  var id=preview.Tag;preview.Focus();dock.KeyPress(Key.Right,Avalonia.Input.RawInputModifiers.Alt,PhysicalKey.ArrowRight,null);Dispatcher.UIThread.RunJobs();
  var moved=dock.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,id));
  if(!moved.IsFocused)throw new InvalidOperationException("Keyboard reorder lost focus");count++;
  dock.KeyPress(Key.Escape,Avalonia.Input.RawInputModifiers.None,PhysicalKey.Escape,null);if(dock.Expanded)throw new InvalidOperationException("Escape must close the panel");count++;
  return count;
 }
 internal static int Performance(string[] args){
  string output=args.Length>1?args[1]:"performance.json";
  using var dock=new DockWindow(true);dock.Show();
  string[] modules=["music","clipboard","camera","cat","all"];
  void Cycle(){foreach(var module in modules){dock.ShowModule(module);dock.Settle();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);}dock.Collapse();dock.Settle();Dispatcher.UIThread.RunJobs();}
  for(int i=0;i<5;i++)Cycle();
  long retainedBefore=GC.GetTotalMemory(true),allocated=GC.GetTotalAllocatedBytes(true);
  using var process=Process.GetCurrentProcess();var cpuBefore=process.TotalProcessorTime;var durations=new List<double>();var watch=Stopwatch.StartNew();
  for(int i=0;i<40;i++){var sample=Stopwatch.StartNew();Cycle();durations.Add(sample.Elapsed.TotalMilliseconds);}
  double elapsed=watch.Elapsed.TotalSeconds,cpu=(process.TotalProcessorTime-cpuBefore).TotalSeconds;
  long allocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated,retainedGrowth=GC.GetTotalMemory(true)-retainedBefore;
  process.Refresh();durations.Sort();
  var result=new{scope="Headless synthetic module switching including forced render ticks; not real desktop idle CPU or camera throughput",os=System.Runtime.InteropServices.RuntimeInformation.OSDescription,architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),cycles=40,moduleSwitches=200,elapsedSeconds=elapsed,cpuSeconds=cpu,cycleP50Ms=durations[20],cycleP95Ms=durations[38],allocatedBytes,retainedGrowthBytes=retainedGrowth,workingSetBytes=process.WorkingSet64};
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(File.ReadAllText(output));
  // Broad leak/hang guards, not promises about performance on user hardware.
  if(retainedGrowth>32*1024*1024||elapsed>90)throw new InvalidOperationException("Synthetic switching exceeded leak/time guard");
  dock.Close();return 0;
 }
}

