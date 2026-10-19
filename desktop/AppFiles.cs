using System;
using System.IO;
namespace TokenDock;

// macOS bundles separate resources from nested executable code for strict signing.
// Unbundled builds and Linux retain the normal publish-directory layout.
static class AppFiles {
 static string? Contents=>OperatingSystem.IsMacOS()&&File.Exists(Path.Combine(AppContext.BaseDirectory,"..","Info.plist"))?Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..")):null;
 internal static string Resources=>Contents is {} contents?Path.Combine(contents,"Resources"):AppContext.BaseDirectory;
 internal static string Camera=>Contents is {} contents?Path.Combine(contents,"Helpers","TokenDockCamera.app","Contents","MacOS","tokendock-camera"):OperatingSystem.IsMacOS()?Path.Combine(AppContext.BaseDirectory,"native","TokenDockCamera.app","Contents","MacOS","tokendock-camera"):Path.Combine(AppContext.BaseDirectory,"native","tokendock-camera");
 internal static string MediaFramework=>Path.Combine(Contents is {} contents?Path.Combine(contents,"Frameworks"):Path.Combine(AppContext.BaseDirectory,"native"),"MediaRemoteAdapter.framework");
}
