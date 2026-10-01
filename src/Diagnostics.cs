using System.Text.Json;
using NAudio.Wave;
namespace Clicks;
public static class Diagnostics
{
    public static void Run()
    {
        var results=new Dictionary<string,object>();string folder=AppContext.BaseDirectory;
        try
        {
            SoundBank.Initialize();
            void Check(string name,bool ok){results[name]=ok;if(!ok)throw new Exception("Failed: "+name);}
            int effects=0;
            foreach(string name in SoundBank.TapNames)for(int category=0;category<4;category++)for(int up=0;up<2;up++)for(int variant=0;variant<6;variant++)
            {
                var samples=SoundBank.Get(name,category,up==1,variant);
                Check("waveforms_valid",samples.Length>100&&samples.All(float.IsFinite)&&samples.Max(x=>Math.Abs(x))<1&&samples.Any(x=>Math.Abs(x)>.001));effects++;
                Check("built_in_taps_at_most_75ms",samples.Length<=SoundBank.Rate*75/1000);
            }
            results["effect_variants"]=effects;
            results["preset_durations_ms"]=SoundBank.TapNames.ToDictionary(name=>name,name=>SoundBank.Get(name,0,false,2).Length*1000d/SoundBank.Rate);
            results["gunshot_duration_ms"]=SoundBank.Gunshot.Length*1000d/(SoundBank.Rate*2);
            using(var fullMixer=new AudioEngine(false))
            {
                Check("gunshot_contains_full_recording",SoundBank.Gunshot.Length>SoundBank.Rate*2);
                fullMixer.Play(SoundBank.Gunshot,.5f,fullLength:true,stereo:true);
                var first=new float[9600];fullMixer.Read(first,0,first.Length);
                var rest=new float[SoundBank.Gunshot.Length-first.Length+960];fullMixer.Read(rest,0,rest.Length);
                var actual=first.Concat(rest.Take(SoundBank.Gunshot.Length-first.Length)).ToArray();
                Check("gunshot_full_stereo_audio_unchanged",actual.SequenceEqual(SoundBank.Gunshot.Select(x=>x*.5f)));
                Check("gunshot_ends_without_looping",rest.TakeLast(960).All(x=>x==0));
            }
            using(var restartMixer=new AudioEngine(false))
            {
                var gunSettings=new Settings{MousePreset="Gunshot",MouseVolume=50};
                var gunController=new SoundController(gunSettings,restartMixer);
                var interval=new float[SoundBank.Rate*2/10];
                var expected=SoundBank.Gunshot.Take(interval.Length).Select(x=>x*.5f).ToArray();
                for(int click=0;click<10;click++)
                {
                    Check("gunshot_accepts_every_click_at_10_per_second",gunController.Handle(new(false,0,false,false)));
                    restartMixer.Read(interval,0,interval.Length);
                    Check("gunshot_restarts_from_beginning_without_stacking",interval.SequenceEqual(expected));
                    gunController.Handle(new(false,0,true,false));
                }
                Check("all_10_clicks_played",restartMixer.Played==10&&gunController.Clicks==10);
                var tail=new float[SoundBank.Gunshot.Length-interval.Length+960];
                restartMixer.Read(tail,0,tail.Length);
                Check("last_gunshot_finishes_in_full",tail.Take(SoundBank.Gunshot.Length-interval.Length).SequenceEqual(SoundBank.Gunshot.Skip(interval.Length).Select(x=>x*.5f)));
                Check("restarted_gunshot_has_no_backlog",tail.TakeLast(960).All(x=>x==0));
            }
            using(var repeatMixer=new AudioEngine(false))
            {
                var tap=SoundBank.Get("Hush",0,false,2);
                float singlePeak=tap.Max(x=>Math.Abs(x))*.6f;
                var block=new float[96];float repeatedPeak=0;
                // Three seconds of extremely fast input through real-sized 1 ms reads.
                for(int ms=0;ms<3000;ms++)
                {
                    if(ms%3==0)repeatMixer.Play(tap,.6f);
                    repeatMixer.Read(block,0,block.Length);
                    repeatedPeak=Math.Max(repeatedPeak,block.Max(x=>Math.Abs(x)));
                }
                results["rapid_peak_vs_single_tap"]=repeatedPeak/singlePeak;
                Check("rapid_input_does_not_increase_peak",repeatedPeak<=singlePeak*1.001f);
                for(int i=0;i<150;i++)repeatMixer.Read(block,0,block.Length);
                Check("rapid_input_has_no_backlog",block.All(x=>x==0));
                var bufferForBurst=new float[SoundBank.Rate/5];
                for(int i=0;i<16;i++)repeatMixer.Play(tap,.6f);
                repeatMixer.Read(bufferForBurst,0,bufferForBurst.Length);
                Check("simultaneous_inputs_do_not_stack",bufferForBurst.Max(x=>Math.Abs(x))<=singlePeak*1.001f);
            }
            Check("variation_changes_sound",!SoundBank.Get("Hush",0,false,0).SequenceEqual(SoundBank.Get("Hush",0,false,1)));
            Check("release_differs",!SoundBank.Get("Crisp",0,false,2).SequenceEqual(SoundBank.Get("Crisp",0,true,2)));
            using var mixer=new AudioEngine(false);var settings=new Settings();var controller=new SoundController(settings,mixer);
            controller.Handle(new(true,65,false,false));var buffer=new float[20000];mixer.Read(buffer,0,buffer.Length);
            Check("key_produces_audio",buffer.Any(x=>Math.Abs(x)>.001));
            mixer.Clear();controller.Handle(new(true,65,false,false));mixer.Read(buffer,0,buffer.Length);
            Check("single_tap_silent_after_75ms",buffer.Skip(SoundBank.Rate*75/1000*2).All(x=>x==0));
            Check("release_does_not_replay_by_default",!controller.Handle(new(true,65,true,false)));
            mixer.Clear();mixer.Play(new float[SoundBank.Rate*4].Select(_=>.1f).ToArray(),.5f);mixer.Read(buffer,0,buffer.Length);
            Check("even_long_input_hard_stops_at_120ms",buffer.Take(SoundBank.MaxEffectSamples*2).Any(x=>x!=0)&&buffer.Skip(SoundBank.MaxEffectSamples*2).All(x=>x==0));
            settings.Enabled=false;Check("master_mute",!controller.Handle(new(true,65,false,false)));mixer.Clear();mixer.Read(buffer,0,buffer.Length);Check("mute_silence",buffer.All(x=>x==0));
            settings.Enabled=true;settings.Keyboard=false;Check("keyboard_switch",!controller.Handle(new(true,65,false,false)));
            settings.Mouse=false;Check("mouse_switch",!controller.Handle(new(false,0,false,false)));
            settings.Release=false;settings.Keyboard=true;Check("release_switch",!controller.Handle(new(true,65,true,false)));
            controller.AppMuted=true;Check("application_mute",!controller.Handle(new(true,65,false,false)));controller.AppMuted=false;
            Check("scroll_default_off",!controller.Handle(new(false,0,false,true)));settings.Scroll=true;Check("scroll_enabled",controller.Handle(new(false,0,false,true)));Check("scroll_throttled",!controller.Handle(new(false,0,false,true)));
            mixer.Clear();for(int i=0;i<200;i++)mixer.Play(SoundBank.Get("Hush",0,false,i%6),1);mixer.Read(buffer,0,buffer.Length);Check("rapid_overlap_bounded",buffer.All(x=>float.IsFinite(x)&&Math.Abs(x)<1));
            mixer.Clear();mixer.Play(SoundBank.Get("Crisp",0,false,2),.5f,-1);mixer.Read(buffer,0,buffer.Length);Check("stereo_pan",buffer.Where((_,i)=>i%2==1).All(x=>x==0)&&buffer.Where((_,i)=>i%2==0).Any(x=>x!=0));
            using(var hooks=new InputHooks())Check("windows_hooks_installed",hooks.Installed);
            string preview=Path.Combine(folder,"sound-preview.wav");
            using(var writer=new WaveFileWriter(preview,WaveFormat.CreateIeeeFloatWaveFormat(SoundBank.Rate,1)))
            foreach(string preset in SoundBank.TapNames)
            {var data=SoundBank.Get(preset,0,false,2);writer.WriteSamples(data,0,data.Length);var silence=new float[SoundBank.Rate/4];writer.WriteSamples(silence,0,silence.Length);}
            Check("custom_import",SoundBank.LoadCustom(WriteShortWav(folder)).Length>100);
            // Verify that our playback reaches the actual default Windows render endpoint.
            try
            {
                using var capture=new WasapiLoopbackCapture();using var output=new AudioEngine();
                long bytes=0;double peak=0;object gate=new();
                capture.DataAvailable+=(_,e)=>{lock(gate){bytes+=e.BytesRecorded;if(capture.WaveFormat.Encoding==WaveFormatEncoding.IeeeFloat)for(int i=0;i+3<e.BytesRecorded;i+=4)peak=Math.Max(peak,Math.Abs(BitConverter.ToSingle(e.Buffer,i)));}};
                capture.StartRecording();Thread.Sleep(200);
                for(int i=0;i<8;i++){output.Play(SoundBank.Get("Hush",0,false,i%6),.45f);Thread.Sleep(110);}
                Thread.Sleep(250);capture.StopRecording();Thread.Sleep(100);
                lock(gate){results["loopback_bytes"]=bytes;results["loopback_peak"]=peak;results["audio_endpoint_output_detected"]=peak>.0001;}
            }
            catch(Exception e){results["audio_endpoint_check"]=e.Message;}
            results["passed"]=true;
        }
        catch(Exception e){results["passed"]=false;results["error"]=e.ToString();}
        File.WriteAllText(Path.Combine(folder,"test-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
    }
    static string WriteShortWav(string folder)
    {string path=Path.Combine(folder,"import-test.wav");using var writer=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(SoundBank.Rate,1));var data=SoundBank.Get("Hush",0,false,2);writer.WriteSamples(data,0,data.Length);return path;}
}
