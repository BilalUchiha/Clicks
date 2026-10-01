using NAudio.Wave;
namespace Clicks;
public sealed class AudioEngine:ISampleProvider,IDisposable
{
    sealed class Voice(float[] data,float left,float right,bool fullLength,bool stereo)
    { public readonly float[] Data=data; public readonly int Channels=stereo?2:1; public readonly int Length=(fullLength?data.Length:Math.Min(data.Length,SoundBank.MaxEffectSamples))/(stereo?2:1); public int Position; public readonly float Left=left,Right=right; }
    readonly object gate=new(); Voice? voice; WaveOutEvent? player;
    const int TransitionFrames=SoundBank.Rate/1000;
    int transitionRemaining;
    float previousLeft,previousRight,transitionLeft,transitionRight;
    public WaveFormat WaveFormat {get;}=WaveFormat.CreateIeeeFloatWaveFormat(SoundBank.Rate,2);
    public string? Error {get;private set;}
    public long Played {get;private set;}
    public AudioEngine(bool output=true) {if(output) Start();}
    public void Start()
    {
        player?.Dispose(); player=new WaveOutEvent{DesiredLatency=36,NumberOfBuffers=3};
        player.PlaybackStopped+=(_,e)=>{if(e.Exception!=null) Error=e.Exception.Message;};
        player.Init(this); player.Play(); Error=null;
    }
    public bool Play(float[] samples,float volume,float pan=0,bool fullLength=false,bool stereo=false)
    {
        if(volume<=0||samples.Length==0) return false; pan=Math.Clamp(pan,-1,1);
        float left=volume*(float)Math.Sqrt(1-pan),right=volume*(float)Math.Sqrt(1+pan);
        lock(gate)
        {
            // Every press replaces the current voice, including full recordings.
            // Full recordings restart exactly; short taps use a tiny transition.
            transitionLeft=previousLeft;transitionRight=previousRight;
            transitionRemaining=!fullLength&&voice!=null?TransitionFrames:0;
            voice=new Voice(samples,fullLength?volume:left,fullLength?volume:right,fullLength,stereo);Played++;
            return true;
        }
    }
    public void Clear()
    {
        lock(gate){voice=null;transitionRemaining=0;previousLeft=previousRight=transitionLeft=transitionRight=0;}
    }
    public int Read(float[] buffer,int offset,int count)
    {
        Array.Clear(buffer,offset,count);
        lock(gate)
        {
            for(int i=0;i<count/2;i++)
            {
                float left=0,right=0;
                if(voice!=null)
                {
                    int index=voice.Position++*voice.Channels;
                    left=voice.Data[index]*voice.Left;right=voice.Data[index+(voice.Channels==2?1:0)]*voice.Right;
                    if(voice.Position>=voice.Length)voice=null;
                }
                if(transitionRemaining>0)
                {
                    float oldWeight=transitionRemaining/(float)TransitionFrames;
                    left=left*(1-oldWeight)+transitionLeft*oldWeight;
                    right=right*(1-oldWeight)+transitionRight*oldWeight;
                    transitionRemaining--;
                }
                previousLeft=left;previousRight=right;
                buffer[offset+i*2]=left;buffer[offset+i*2+1]=right;
            }
        }
        for(int i=offset;i<offset+count;i++) buffer[i]=Math.Clamp(buffer[i],-1f,1f);
        return count;
    }
    public void Dispose() {player?.Stop();player?.Dispose();}
}
