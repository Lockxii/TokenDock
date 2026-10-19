using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace TokenDock;

sealed class MusicModule:IDisposable {
 readonly Dispatcher dispatcher;GlobalSystemMediaTransportControlsSessionManager? manager;GlobalSystemMediaTransportControlsSession? session;bool enabled;int revision;
 public string Title {get;private set;}="Aucune lecture";public string Artist {get;private set;}="";public string Status {get;private set;}="Désactivé";
 public bool Playing {get;private set;}public bool CanPlay {get;private set;}public bool CanNext {get;private set;}public bool CanPrevious {get;private set;}public bool CanSeek {get;private set;}
 public BitmapSource? Artwork {get;private set;}public MusicTimeline Timeline {get;private set;}=MusicTimeline.Empty;
 public event Action? Changed;
 public MusicModule(Dispatcher dispatcher)=>this.dispatcher=dispatcher;
 public async Task Enable(bool value){
  if(enabled==value)return;enabled=value;revision++;
  if(!value){Detach();if(manager!=null)manager.CurrentSessionChanged-=SessionChanged;manager=null;Clear("Désactivé");return;}
  int request=revision;try{var result=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();if(!enabled||request!=revision)return;manager=result;manager.CurrentSessionChanged+=SessionChanged;Bind();}catch(Exception e)when(MediaError(e)){if(enabled&&request==revision)Clear("Contrôle multimédia indisponible");}
 }
 static bool MediaError(Exception e)=>e is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or IOException or ArgumentException;
 void Dispatch(Action action){if(dispatcher.HasShutdownStarted)return;try{dispatcher.BeginInvoke(()=>{if(enabled)action();});}catch(InvalidOperationException){}}
 void SessionChanged(GlobalSystemMediaTransportControlsSessionManager sender,CurrentSessionChangedEventArgs args)=>Dispatch(Bind);
 void MediaChanged(GlobalSystemMediaTransportControlsSession sender,MediaPropertiesChangedEventArgs args)=>Dispatch(()=>{if(session==sender)_=RefreshMedia();});
 void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender,PlaybackInfoChangedEventArgs args)=>Dispatch(()=>{if(session==sender)RefreshPlayback();});
 void TimelineChanged(GlobalSystemMediaTransportControlsSession sender,TimelinePropertiesChangedEventArgs args)=>Dispatch(()=>{if(session==sender)RefreshPlayback();});
 void Detach(){revision++;if(session!=null){session.MediaPropertiesChanged-=MediaChanged;session.PlaybackInfoChanged-=PlaybackChanged;session.TimelinePropertiesChanged-=TimelineChanged;session=null;}}
 void Bind(){Detach();try{session=manager?.GetCurrentSession();Clear("Aucune lecture active");if(session!=null){session.MediaPropertiesChanged+=MediaChanged;session.PlaybackInfoChanged+=PlaybackChanged;session.TimelinePropertiesChanged+=TimelineChanged;RefreshPlayback();_=RefreshMedia();}}catch(Exception e)when(MediaError(e)){Clear("Session momentanément indisponible");}}
 void RefreshPlayback(){var current=session;if(!enabled||current==null)return;try{
  var playback=current.GetPlaybackInfo();var timeline=current.GetTimelineProperties();Playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;CanPlay=playback.Controls.IsPlayPauseToggleEnabled;CanNext=playback.Controls.IsNextEnabled;CanPrevious=playback.Controls.IsPreviousEnabled;
  var start=timeline.StartTime<TimeSpan.Zero?TimeSpan.Zero:timeline.StartTime;var end=timeline.EndTime<start?start:timeline.EndTime;
  Timeline=new(start,end,timeline.Position,timeline.LastUpdatedTime,playback.PlaybackRate??1,Playing,timeline.MinSeekTime,timeline.MaxSeekTime);CanSeek=playback.Controls.IsPlaybackPositionEnabled&&Timeline.SeekTicks(.5)!=null;Status=Playing?"En lecture":"En pause";Changed?.Invoke();
 }catch(Exception e)when(MediaError(e)){CanPlay=CanNext=CanPrevious=CanSeek=Playing=false;Timeline=MusicTimeline.Empty;Status="Session momentanément indisponible";Changed?.Invoke();}}
 async Task RefreshMedia(){var current=session;int request=++revision;if(current==null)return;try{
  var properties=await current.TryGetMediaPropertiesAsync();if(!enabled||request!=revision||session!=current)return;
  Title=string.IsNullOrWhiteSpace(properties.Title)?"Lecture en cours":properties.Title;Artist=properties.Artist;Artwork=null;RefreshPlayback();
  if(properties.Thumbnail is not { } thumbnail)return;
  BitmapSource? cover=null;
  try{using var stream=await thumbnail.OpenReadAsync();if(stream.Size>0&&stream.Size<=4*1024*1024){using var input=stream.GetInputStreamAt(0);using var reader=new DataReader(input);uint size=(uint)stream.Size;if(await reader.LoadAsync(size)==size){var bytes=new byte[size];reader.ReadBytes(bytes);cover=await Task.Run(()=>DecodeArtwork(bytes));}}}catch(Exception e)when(MediaError(e)||e is FormatException){/* A missing/broken cover must not hide transport controls. */}
  if(enabled&&request==revision&&session==current){Artwork=cover;Changed?.Invoke();}
 }catch(Exception e)when(MediaError(e)){if(enabled&&request==revision&&session==current){Artwork=null;Status="Métadonnées indisponibles";Changed?.Invoke();}}}
 internal static BitmapSource? DecodeArtwork(byte[] bytes){if(bytes.Length==0||bytes.Length>4*1024*1024)return null;try{using var stream=new MemoryStream(bytes,false);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=192;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}catch(Exception e)when(e is IOException or NotSupportedException or FormatException or ArgumentException){return null;}}
 void Clear(string status){Title="Aucune lecture";Artist="";Artwork=null;Timeline=MusicTimeline.Empty;Playing=CanPlay=CanNext=CanPrevious=CanSeek=false;Status=status;Changed?.Invoke();}
 public async Task Command(string action){var current=session;if(current==null||!enabled)return;try{bool ok=action switch{"previous"=>CanPrevious&&await current.TrySkipPreviousAsync(),"next"=>CanNext&&await current.TrySkipNextAsync(),_=>CanPlay&&await current.TryTogglePlayPauseAsync()};if(!enabled||current!=session)return;if(!ok){Status="Commande non prise en charge par le lecteur";Changed?.Invoke();}else RefreshPlayback();}catch(Exception e)when(MediaError(e)){if(enabled&&current==session){Status="Lecteur indisponible";Changed?.Invoke();}}}
 public async Task Seek(double fraction){var current=session;var ticks=Timeline.SeekTicks(fraction);if(current==null||!enabled||!CanSeek||ticks==null)return;int track=revision;try{bool ok=await current.TryChangePlaybackPositionAsync(ticks.Value);if(!enabled||current!=session||track!=revision)return;if(ok){Timeline=Timeline with{Position=TimeSpan.FromTicks(ticks.Value),Updated=DateTimeOffset.UtcNow};Changed?.Invoke();}else{Status="Déplacement non pris en charge";Changed?.Invoke();}}catch(Exception e)when(MediaError(e)){if(enabled&&current==session){Status="Déplacement indisponible";Changed?.Invoke();}}}
 internal void SetFixture(BitmapSource? cover,MusicTimeline timeline){Title="Nocturne";Artist="Session de démonstration";Status="En lecture";Artwork=cover;Timeline=timeline;Playing=timeline.Playing;CanPlay=CanNext=CanPrevious=true;CanSeek=timeline.SeekTicks(.5)!=null;Changed?.Invoke();}
 public void Dispose(){enabled=false;Detach();if(manager!=null)manager.CurrentSessionChanged-=SessionChanged;manager=null;Artwork=null;}
}

sealed class MusicView:StackPanel,IDisposable {
 readonly MusicModule module;
 public MusicView(MusicModule module){this.module=module;Resources=PanelUi.Theme();module.Changed+=Refresh;Refresh();}
 void Refresh(){Children.Clear();Children.Add(PanelUi.Label("♫",52,PanelUi.Mint));Children.Add(PanelUi.Label(module.Title,24));Children.Add(PanelUi.Label(module.Artist,14,PanelUi.Muted));Children.Add(PanelUi.Label(module.Status,12,PanelUi.Muted));var row=new StackPanel{Orientation=Orientation.Horizontal};foreach(var (text,action,can) in new[]{("Précédent","previous",module.CanPrevious),(module.Playing?"Pause":"Lecture","toggle",module.CanPlay),("Suivant","next",module.CanNext)}){var button=PanelUi.Button(text,()=>_=module.Command(action));button.IsEnabled=can;row.Children.Add(button);}Children.Add(row);Children.Add(PanelUi.Label("Le lecteur actif est choisi par Windows. Les commandes dépendent des fonctions exposées par l’application.",11,PanelUi.Muted));}
 public void Dispose()=>module.Changed-=Refresh;
}
