namespace Clicks;

// Independently implemented DSP using the acoustic parameters observed in
// Clacky's public browser previews. No website JavaScript executes in this app.
public static class ClackyStyleSynth
{
    public sealed record Noise(string Filter,double Hz,double Q,double Duration,double Level=1,double Delay=0);
    public sealed record Tone(double Hz,double Duration,double Level,double Glide=1,double Delay=0,string Wave="sine");
    public sealed record Profile(Noise Snap,double Gain,Tone? Body=null,Noise? Air=null,Tone? Ping=null,Noise? Hit=null,Tone[]? Partials=null,double ReleaseLevel=.5,double ReleaseBrightness=1.3,double ReleasePitch=1.2);
    public static readonly Dictionary<string,Profile> Profiles=new()
    {
        ["Hush"]=new(new("lowpass",1300,.7,.030),.55,new(620,.020,.6,.6,Wave:"triangle")),
        ["Felt"]=new(new("lowpass",800,0,.016),1.04,new(300,.014,.7,.7),ReleaseLevel:.4),
        ["Tap"]=new(new("lowpass",520,.9,.050),.95,new(190,.050,.6,.6,Wave:"triangle")),
        ["Thock"]=new(new("bandpass",900,1.2,.020),.55,new(240,.035,.9,.72),new("bandpass",480,4,.030,.6),Hit:new("lowpass",1800,0,.005,.3,.003),ReleaseLevel:.45,ReleaseBrightness:1.45,ReleasePitch:1.25),
        ["Walnut"]=new(new("bandpass",1200,4,.025),.9,new(300,.020,.5,.6,Wave:"triangle"),Ping:new(820,.040,.5,.97)),
        ["Crisp"]=new(new("bandpass",3800,1.1,.018),.9,new(2300,.012,.6,.6,Wave:"triangle")),
        ["Bubble"]=new(new("lowpass",1500,0,.004,.25),.46,Ping:new(520,.060,1,2.4),ReleaseLevel:.45,ReleasePitch:1.35),
        ["Tok"]=new(new("bandpass",2600,2.5,.012),.34,Air:new("bandpass",1250,8,.050,.3),Ping:new(1250,.070,.9,.985),Partials:[new(3380,.030,.35),new(5900,.012,.15)])
    };
    public static float[] Render(string name,int category,bool up,int variation)
    {
        var profile=Profiles[name];
        double pitch=1+(variation-2.5)*.012;
        double level=profile.Gain*.8*.85;
        if(category==1)pitch*=.96;
        if(category==2)pitch*=1.035;
        double brightness=up?profile.ReleaseBrightness:1;
        double bodyPitch=up?profile.ReleasePitch:1;
        if(up)level*=profile.ReleaseLevel;
        double duration=new[]{profile.Snap.Duration,profile.Body?.Duration??0,profile.Air?.Duration??0,profile.Ping?.Duration??0,profile.Hit is {} h?h.Delay+h.Duration:0,profile.Partials?.Max(t=>t.Delay+t.Duration)??0}.Max()+.004;
        if(category==3)duration=.018;
        var output=new double[(int)Math.Ceiling(duration*SoundBank.Rate)];
        var random=new Random(92873+variation*389+(up?217:0)+category*137);
        if(category==3)
        {
            NoisePart(output,new("bandpass",1800,1,.008,.32),pitch,level,random);
        }
        else
        {
            NoisePart(output,profile.Snap,pitch*brightness,level,random);
            if(profile.Air is {} air)NoisePart(output,air,pitch*brightness,level,random);
            if(profile.Body is {} body)TonePart(output,body,pitch*bodyPitch,level);
            if(profile.Ping is {} ping)TonePart(output,ping,pitch*bodyPitch,level);
            if(profile.Hit is {} hit)NoisePart(output,hit,pitch*brightness,level,random);
            if(profile.Partials is {} partials)foreach(var partial in partials)TonePart(output,partial,pitch*bodyPitch,level);
        }
        return output.Select(v=>(float)v).ToArray();
    }
    static double Envelope(double time,double duration,double amplitude)
    {
        if(time<0||time>=duration+.003)return 0;
        if(time<.002)return amplitude*time/.002;
        if(time<duration)return amplitude*Math.Pow(.0001/Math.Max(.0001,amplitude),(time-.002)/Math.Max(.001,duration-.002));
        return .0001*(1-(time-duration)/.003);
    }
    static void NoisePart(double[] output,Noise effect,double pitch,double gain,Random random)
    {
        var filter=new Biquad(effect.Filter,effect.Hz*pitch,effect.Q);
        int start=(int)Math.Round(effect.Delay*SoundBank.Rate);
        for(int i=start;i<output.Length;i++)
        {
            double time=(i-start)/(double)SoundBank.Rate;
            output[i]+=filter.Process(random.NextDouble()*2-1)*Envelope(time,effect.Duration,gain*effect.Level);
        }
    }
    static void TonePart(double[] output,Tone effect,double pitch,double gain)
    {
        double phase=0;int start=(int)Math.Round(effect.Delay*SoundBank.Rate);
        for(int i=start;i<output.Length;i++)
        {
            double time=(i-start)/(double)SoundBank.Rate;
            double wave=effect.Wave=="triangle"?2/Math.PI*Math.Asin(Math.Sin(phase)):Math.Sin(phase);
            output[i]+=wave*Envelope(time,effect.Duration,gain*effect.Level);
            double hz=effect.Hz*pitch*Math.Pow(effect.Glide,Math.Min(1,time/effect.Duration));
            phase+=2*Math.PI*hz/SoundBank.Rate;
        }
    }
    sealed class Biquad
    {
        readonly double b0,b1,b2,a1,a2;double z1,z2;
        public Biquad(string type,double hz,double q)
        {
            double omega=2*Math.PI*Math.Min(hz,SoundBank.Rate*.49)/SoundBank.Rate;
            double cosine=Math.Cos(omega),sine=Math.Sin(omega);
            // Web Audio interprets low/high-pass Q in dB, band-pass Q as linear.
            double alpha=sine/(2*(type=="bandpass"?Math.Max(.01,q):Math.Pow(10,q/20)));
            double denominator=1+alpha;
            (double x,double y,double z)=type switch
            {
                "lowpass"=>((1-cosine)/2,1-cosine,(1-cosine)/2),
                "highpass"=>((1+cosine)/2,-(1+cosine),(1+cosine)/2),
                _=>(alpha,0,-alpha)
            };
            b0=x/denominator;b1=y/denominator;b2=z/denominator;a1=-2*cosine/denominator;a2=(1-alpha)/denominator;
        }
        public double Process(double value)
        {double next=b0*value+z1;z1=b1*value-a1*next+z2;z2=b2*value-a2*next;return next;}
    }
}
