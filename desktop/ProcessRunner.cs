using System.Diagnostics;
using System.Text;
namespace TokenDock;

// Commands are fixed executable names and ArgumentList values, never shell strings.
static class ProcessRunner {
 public static async Task<string> Run(string executable,IEnumerable<string> arguments,CancellationToken cancel=default,int maximum=65536){
  var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in arguments)start.ArgumentList.Add(arg);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromSeconds(4));
  using var process=new Process{StartInfo=start};
  if(!process.Start())throw new IOException("Cannot start native adapter.");
  try{
   var output=Read(process.StandardOutput,maximum,timeout.Token);var error=Read(process.StandardError,8192,timeout.Token);
   await Task.WhenAll(output,error,process.WaitForExitAsync(timeout.Token));
   if(process.ExitCode!=0)throw new IOException("Native adapter unavailable: "+(await error).Trim());
   return (await output).Trim();
  }finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync(CancellationToken.None);}}
 }
 static async Task<string> Read(StreamReader stream,int maximum,CancellationToken token){
  char[] buffer=new char[2048];var result=new StringBuilder();int count;
  while((count=await stream.ReadAsync(buffer,token))!=0){if(result.Length+count>maximum)throw new IOException("Native adapter response exceeded its limit.");result.Append(buffer,0,count);}
  return result.ToString();
 }
}
