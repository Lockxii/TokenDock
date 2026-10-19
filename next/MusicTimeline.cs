namespace TokenDock;

record MusicTimeline(TimeSpan Start,TimeSpan End,TimeSpan Position,DateTimeOffset Updated,double Rate,bool Playing,TimeSpan MinSeek,TimeSpan MaxSeek){
 public static MusicTimeline Empty=>new(TimeSpan.Zero,TimeSpan.Zero,TimeSpan.Zero,default,1,false,TimeSpan.Zero,TimeSpan.Zero);
 public TimeSpan Duration=>End>Start?End-Start:TimeSpan.Zero;
 public TimeSpan At(DateTimeOffset now){if(Duration<=TimeSpan.Zero)return Start;double elapsed=Playing&&Updated!=default?Math.Max(0,(now-Updated).TotalSeconds):0;double rate=double.IsFinite(Rate)&&Rate>=0?Rate:1;double ticks=Position.Ticks+elapsed*rate*TimeSpan.TicksPerSecond;if(ticks>=End.Ticks)return End;if(ticks<=Start.Ticks)return Start;return TimeSpan.FromTicks((long)ticks);}
 public long? SeekTicks(double fraction){if(!double.IsFinite(fraction)||Duration<=TimeSpan.Zero)return null;long low=Math.Max(Start.Ticks,MinSeek.Ticks),high=Math.Min(End.Ticks,MaxSeek.Ticks);if(low>=high)return null;long target=Start.Ticks+(long)((decimal)Duration.Ticks*(decimal)Math.Clamp(fraction,0,1));return Math.Clamp(target,low,high);}
 public static string Clock(TimeSpan time)=>time.TotalHours>=1?$"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}":$"{Math.Max(0,(int)time.TotalMinutes)}:{Math.Max(0,time.Seconds):00}";
}
