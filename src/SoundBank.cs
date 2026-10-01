using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace Clicks;
public static class SoundBank
{
    public const int Rate=48000;
    public const int MaxEffectSamples=Rate*120/1000;
    public static readonly string[] TapNames=["Hush","Felt","Tap","Thock","Walnut","Crisp","Bubble","Tok"];
    public static readonly string[] Names=[..TapNames,"Gunshot"];
    public static readonly string[] Descriptions=["Clacky-style Hush: a soft, muffled click.","Clacky-style Felt: a tiny tap through cloth.","Clacky-style Tap: a deep, round trackpad thud.","Clacky-style Thock: a creamy, bassy impact.","Clacky-style Walnut: a warm wooden knock.","Clacky-style Crisp: a quick, bright snap.","Clacky-style Bubble: a short water pop.","Clacky-style Tok: a hollow wooden tap.","Your full gunshot MP3. Plays to the end, unchanged."];
    public static float[] Gunshot {get;private set;}=[];
    static readonly Dictionary<string,float[][]> cache=new();
    public static void Initialize()
    {
        LoadGunshot();
        foreach(string name in TapNames) for(int category=0;category<4;category++) for(int up=0;up<2;up++)
        {
            var variants=Enumerable.Range(0,6).Select(v=>ClackyStyleSynth.Render(name,category,up==1,v)).ToArray();
            var energies=variants.Select(data=>data.Sum(x=>(double)x*x)).ToArray();
            double target=energies.Order().ElementAt(2);
            for(int v=0;v<variants.Length;v++)
            {
                float adjustment=(float)Math.Sqrt(target/Math.Max(1e-12,energies[v]));
                for(int i=0;i<variants[v].Length;i++)variants[v][i]*=adjustment;
            }
            cache[$"{name}:{category}:{up}"]=variants;
        }
    }
    static void LoadGunshot()
    {
        using var stream=typeof(SoundBank).Assembly.GetManifestResourceStream("Clicks.Gunshot.mp3")??throw new InvalidOperationException("Gunshot file is missing.");
        using var reader=new Mp3FileReader(stream);
        ISampleProvider input=reader.ToSampleProvider();
        if(input.WaveFormat.Channels!=2)throw new InvalidOperationException("The gunshot should be stereo.");
        if(input.WaveFormat.SampleRate!=Rate)input=new WdlResamplingSampleProvider(input,Rate);
        var full=new List<float>();var buffer=new float[4096];int n;
        while((n=input.Read(buffer,0,buffer.Length))>0)full.AddRange(buffer.Take(n));
        Gunshot=full.ToArray();
    }
    static float[] Shape(float[] source,double speed,int milliseconds,bool cushion,float gain)
    {
        int size=Math.Min((int)(source.Length/speed),Rate*milliseconds/1000);
        var result=new float[size];float low=0;
        for(int i=0;i<size;i++)
        {
            double position=i*speed;int index=(int)position;float fraction=(float)(position-index);
            float value=source[index]*(1-fraction)+source[Math.Min(index+1,source.Length-1)]*fraction;
            if(cushion){low+=.10f*(value-low);value=low;}
            float fade=Math.Min(1f,(size-1-i)/192f)*Math.Min(1f,i/24f);
            result[i]=value*fade*gain;
        }
        return result;
    }
    public static float[] Get(string name,int category,bool up,int variant)=>cache[$"{name}:{category}:{(up?1:0)}"][variant%6];
    public static float[] LoadCustom(string path)
    {
        using var reader=new AudioFileReader(path);
        if(reader.TotalTime.TotalSeconds>2)throw new InvalidOperationException("Choose a source file up to 2 seconds. Only a short 120 ms tap will be used.");
        ISampleProvider input=reader;
        if(input.WaveFormat.Channels==2)input=new StereoToMonoSampleProvider(input);
        if(input.WaveFormat.Channels!=1)throw new InvalidOperationException("Use a mono or stereo file.");
        if(input.WaveFormat.SampleRate!=Rate)input=new WdlResamplingSampleProvider(input,Rate);
        var all=new List<float>();var buffer=new float[4096];int n;
        while((n=input.Read(buffer,0,buffer.Length))>0)all.AddRange(buffer.Take(n));
        if(all.Count==0)throw new InvalidOperationException("This audio file is empty.");
        float peak=all.Max(x=>Math.Abs(x));
        if(peak<.00001f)throw new InvalidOperationException("This audio file is silent.");
        int onset=Math.Max(0,all.FindIndex(x=>Math.Abs(x)>=peak*.08f)-24);
        var tap=all.Skip(onset).Take(MaxEffectSamples).Select(x=>x*.48f/peak).ToArray();
        return Shape(tap,1,120,false,1);
    }
}
