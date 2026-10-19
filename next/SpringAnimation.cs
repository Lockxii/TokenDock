using System.Windows;
using System.Windows.Media.Animation;
namespace TokenDock;

// Analytic critically damped motion: no polling loop, and retargeting preserves velocity.
sealed class SpringAnimation:DoubleAnimationBase {
 public static readonly DependencyProperty FromProperty=DependencyProperty.Register(nameof(From),typeof(double),typeof(SpringAnimation),new PropertyMetadata(0d));
 public static readonly DependencyProperty ToProperty=DependencyProperty.Register(nameof(To),typeof(double),typeof(SpringAnimation),new PropertyMetadata(0d));
 public static readonly DependencyProperty VelocityProperty=DependencyProperty.Register(nameof(Velocity),typeof(double),typeof(SpringAnimation),new PropertyMetadata(0d));
 public static readonly DependencyProperty FrequencyProperty=DependencyProperty.Register(nameof(Frequency),typeof(double),typeof(SpringAnimation),new PropertyMetadata(16d));
 public double From{get=>(double)GetValue(FromProperty);set=>SetValue(FromProperty,value);}
 public double To{get=>(double)GetValue(ToProperty);set=>SetValue(ToProperty,value);}
 public double Velocity{get=>(double)GetValue(VelocityProperty);set=>SetValue(VelocityProperty,value);}
 public double Frequency{get=>(double)GetValue(FrequencyProperty);set=>SetValue(FrequencyProperty,value);}
 internal (double Position,double Velocity) Sample(double seconds){
  if(Duration.HasTimeSpan&&seconds>=Duration.TimeSpan.TotalSeconds)return(To,0);
  double t=Math.Max(0,seconds),offset=From-To,b=Velocity+Frequency*offset,decay=Math.Exp(-Frequency*t);
  return(To+(offset+b*t)*decay,(Velocity-Frequency*b*t)*decay);
 }
 protected override double GetCurrentValueCore(double origin,double destination,AnimationClock clock)=>Sample(clock.CurrentTime?.TotalSeconds??0).Position;
 protected override Freezable CreateInstanceCore()=>new SpringAnimation();
}
