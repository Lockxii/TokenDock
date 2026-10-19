using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
namespace TokenDock;

static class Program {
 [STAThread]public static int Main(string[] args){
  if(args.Contains("--check")){
   AppBuilder.Configure<DockApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
   return DesktopChecks.Run(args);
  }
  return AppBuilder.Configure<DockApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args,ShutdownMode.OnExplicitShutdown);
 }
}
sealed class DockApp:Application {
 public override void Initialize(){Name="TokenDock";RequestedThemeVariant=ThemeVariant.Dark;Styles.Add(new FluentTheme());}
 public override void OnFrameworkInitializationCompleted(){
  if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){var dock=new DockWindow();desktop.MainWindow=dock;desktop.Exit+=(_,_)=>dock.Dispose();dock.Show();}
  base.OnFrameworkInitializationCompleted();
 }
}
