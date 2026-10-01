namespace Clicks;
public sealed class SoundController(Settings settings,AudioEngine audio)
{
    public bool AppMuted {get;set;}
    public long Keys {get;private set;} public long Clicks {get;private set;} public long Scrolls {get;private set;}
    public float[]? CustomKey,CustomMouse;long lastScroll;int variant;
    public bool Handle(InputEvent e)
    {
        if(!settings.Enabled||AppMuted) return false;
        if(e.Keyboard&&!settings.Keyboard || !e.Keyboard&&!e.Scroll&&!settings.Mouse || e.Scroll&&!settings.Scroll) return false;
        if(e.Up&&!settings.Release) return false;
        string preset=e.Keyboard?settings.KeyPreset:settings.MousePreset;float[]? custom=e.Keyboard?CustomKey:CustomMouse;
        if(preset=="Gunshot")
        {
            if(e.Up||e.Scroll)return false;
            bool accepted=audio.Play(SoundBank.Gunshot,(e.Keyboard?settings.KeyVolume:settings.MouseVolume)/100f,fullLength:true,stereo:true);
            if(accepted){if(e.Keyboard)Keys++;else Clicks++;}
            return accepted;
        }
        if(e.Scroll) {long now=Environment.TickCount64;if(now-lastScroll<35) return false;lastScroll=now;Scrolls++;}
        else if(!e.Up) {if(e.Keyboard) Keys++;else Clicks++;}
        if(preset=="Custom"&&custom==null) preset=e.Keyboard?"Hush":"Felt";
        int category=e.Scroll?3:e.Keyboard?e.Code is 32 or 13 or 8?1:0:e.Code==1?2:0;
        int v=settings.Variation?(++variant%6):2;
        float[] data=preset=="Custom"?custom!:SoundBank.Get(preset,category,e.Up,v);
        float volume=(e.Scroll?settings.ScrollVolume:e.Keyboard?settings.KeyVolume:settings.MouseVolume)/100f;
        if(e.Up) volume*=settings.ReleaseVolume/100f;
        float pan=0;
        if(settings.Stereo)
        {
            if(e.Keyboard) pan=e.Code is 81 or 65 or 90 or 87 or 83 or 88 or 69 or 68 or 67?-.28f:e.Code is 73 or 75 or 77 or 79 or 76 or 80 or 13 or 8?.28f:0;
            else {var area=SystemInformation.VirtualScreen;pan=Math.Clamp(((e.X-area.Left)/(float)Math.Max(1,area.Width)*2-1)*.75f,-.75f,.75f);}
        }
        audio.Play(data,volume,pan);return true;
    }
}
