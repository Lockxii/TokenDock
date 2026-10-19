using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
namespace TokenDock;

static class Program {
 internal static bool NativeClipboardCheck;
 [STAThread]public static int Main(string[] args){
  System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
  if(args.Contains("--check")||args.Contains("--media-check")||args.Contains("--media-empty-check")){
   AppBuilder.Configure<DockApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
   return args.Contains("--media-empty-check")?MediaChecks.Empty():args.Contains("--media-check")?MediaChecks.Run():DesktopChecks.Run(args);
  }
  NativeClipboardCheck=args.Contains("--native-clipboard-check");
  if(NativeClipboardCheck&&(Environment.GetEnvironmentVariable("TOKENDOCK_TEST_DISPLAY")!="isolated"||Environment.GetEnvironmentVariable("CI")!="true"))throw new InvalidOperationException("Native clipboard checks are CI-only.");
  return AppBuilder.Configure<DockApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args,ShutdownMode.OnExplicitShutdown);
 }
}
sealed class DockApp:Application {
 public override void Initialize(){Name="TokenDock";RequestedThemeVariant=ThemeVariant.Dark;Styles.Add(new FluentTheme());}
 public override void OnFrameworkInitializationCompleted(){
  if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){var dock=new DockWindow(Program.NativeClipboardCheck);desktop.MainWindow=dock;desktop.Exit+=(_,_)=>dock.Dispose();if(Program.NativeClipboardCheck)dock.Opened+=async(_,_)=>{try{await NativeClipboardChecks.Run(dock).WaitAsync(TimeSpan.FromSeconds(15));desktop.Shutdown(0);}catch(Exception e){Console.Error.WriteLine(e);desktop.Shutdown(1);}};}
  base.OnFrameworkInitializationCompleted();
 }
}
