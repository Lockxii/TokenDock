namespace TokenDock;

// Session-owned copies; originals are never moved or deleted.
sealed class FileShelf:IDisposable {
 readonly string root=Path.Combine(Path.GetTempPath(),"TokenDock-shelf",Guid.NewGuid().ToString("N"));
 readonly SemaphoreSlim gate=new(1);long used;bool disposed;
 public async Task<CopyItem> Store(string[] paths){
  if(CopyItem.FromFiles(paths)==null)throw new IOException("Dépôt invalide (100 fichiers maximum).");
  await gate.WaitAsync();string? batch=null;
  try{
   if(disposed)throw new IOException("La session est fermée.");
   const long limit=256L*1024*1024;long copied=0;
   batch=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(batch);var saved=new List<string>();
   foreach(var path in paths){
    if(!File.Exists(path)||(File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)throw new IOException("Dépose des fichiers classiques, pas des dossiers ou raccourcis système.");
    string folder=Path.Combine(batch,saved.Count.ToString());Directory.CreateDirectory(folder);string target=Path.Combine(folder,Path.GetFileName(path));
    await using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);
    if(input.Length>limit-used-copied)throw new IOException("Stockage temporaire limité à 256 Mo par session.");
    await using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);
    byte[] buffer=new byte[81920];int count;
    while((count=await input.ReadAsync(buffer))>0){copied+=count;if(copied>limit-used)throw new IOException("Stockage temporaire plein.");await output.WriteAsync(buffer.AsMemory(0,count));}
    saved.Add(target);
   }
   var item=CopyItem.FromFiles(saved.ToArray())??throw new IOException("Noms de fichiers trop longs.");used+=copied;return item;
  }catch{if(batch!=null)Remove(batch);throw;}finally{gate.Release();}
 }
 static void Remove(string path){try{if(Directory.Exists(path))Directory.Delete(path,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
 public void Dispose(){disposed=true;_ = Cleanup();}
 async Task Cleanup(){await gate.WaitAsync();try{Remove(root);}finally{gate.Release();}}
}
