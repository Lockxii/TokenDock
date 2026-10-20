using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.Threading;
namespace TokenDock;

static class Program {
 internal static string? AccessibilityDirectory;
 internal static bool NativeClipboardCheck;internal static string[]? NativeDragArgs;
 [STAThread]public static int Main(string[] args){
  if(args.Length==2&&args[0]=="--denied-shelf-check"){
   if(Environment.GetEnvironmentVariable("CI")!="true"||Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated"||Path.GetFileName(args[1])!="denied.txt"||!File.Exists(Path.Combine(Path.GetDirectoryName(args[1])!,"fixture.marker")))throw new InvalidOperationException("Isolated synthetic permission fixture required.");
   if(!File.Exists(args[1]))throw new InvalidOperationException("Fixture metadata must be readable.");
   using var shelf=new FileShelf();try{Task.Run(()=>shelf.Store([args[1]])).GetAwaiter().GetResult();throw new InvalidOperationException("OS sandbox failed to deny the import.");}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Console.WriteLine("PASS: real OS sandbox denial reaches file shelf without importing data.");return 0;}
  }
  System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
  if(args.Contains("--perf-check")||args.Contains("--check")||args.Contains("--media-check")||args.Contains("--media-empty-check")){
   AppBuilder.Configure<DockApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
   return args.Contains("--perf-check")?QualityChecks.Performance(args):args.Contains("--media-empty-check")?MediaChecks.Empty():args.Contains("--media-check")?MediaChecks.Run():DesktopChecks.Run(args);
  }
  if(args.Length==3&&args[0]=="--native-drag-check"){
   if(Environment.GetEnvironmentVariable("CI")!="true"||Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated"||args[1] is not("source" or "sink"))throw new InvalidOperationException("Native drag tests are CI-only.");
   NativeDragArgs=args;
  }
  if(args.Length==2&&args[0]=="--accessibility-fixture"){
   if(Environment.GetEnvironmentVariable("CI")!="true"||Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated")throw new InvalidOperationException("Accessibility fixtures are CI-only.");
   AccessibilityDirectory=Path.GetFullPath(args[1]);
  }
  NativeClipboardCheck=args.Contains("--native-clipboard-check");
  if(NativeClipboardCheck&&(Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated"||Environment.GetEnvironmentVariable("CI")!="true"))throw new InvalidOperationException("Native clipboard checks are CI-only.");
  return AppBuilder.Configure<DockApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args,ShutdownMode.OnExplicitShutdown);
 }
}
sealed class DockApp:Application {
 public override void Initialize(){Name="TokenDock";RequestedThemeVariant=ThemeVariant.Dark;Styles.Add(new FluentTheme());}
 public override void OnFrameworkInitializationCompleted(){
  if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime dragApp&&Program.NativeDragArgs is {} dragArgs){dragApp.MainWindow=new NativeDragWindow(dragArgs[1],dragArgs[2]);base.OnFrameworkInitializationCompleted();return;}
  if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){var dock=new DockWindow(Program.NativeClipboardCheck||Program.AccessibilityDirectory!=null);desktop.MainWindow=dock;desktop.Exit+=(_,_)=>dock.Dispose();if(Program.AccessibilityDirectory is {} directory)AccessibilityFixture.Start(dock,directory,desktop);if(Program.NativeClipboardCheck)dock.Opened+=(_,_)=>Dispatcher.UIThread.Post(async()=>{try{await NativeClipboardChecks.Run(dock).WaitAsync(TimeSpan.FromSeconds(15));desktop.Shutdown(0);}catch(Exception e){Console.Error.WriteLine(e);desktop.Shutdown(1);}},DispatcherPriority.Background);}
  base.OnFrameworkInitializationCompleted();
 }
}
