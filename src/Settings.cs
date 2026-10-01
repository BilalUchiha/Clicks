using System.Text.Json;
using Microsoft.Win32;
namespace Clicks;
public sealed class Settings
{
    public int SoundRevision { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Keyboard { get; set; } = true;
    public bool Mouse { get; set; } = true;
    public bool Scroll { get; set; }
    public bool Release { get; set; }
    public bool Stereo { get; set; }
    public bool Variation { get; set; } = true;
    public bool Ripples { get; set; }
    public string KeyPreset { get; set; } = "Hush";
    public string MousePreset { get; set; } = "Felt";
    public int KeyVolume { get; set; } = 60;
    public int MouseVolume { get; set; } = 65;
    public int ScrollVolume { get; set; } = 18;
    public int ReleaseVolume { get; set; } = 40;
    public string CustomKey { get; set; } = "";
    public string CustomMouse { get; set; } = "";
    public string MutedApps { get; set; } = "";
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clicks");
    public static readonly string FileName = Path.Combine(Folder, "settings.json");
    public static Settings Load()
    {
        try {
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FileName)) ?? new();
            if(s.SoundRevision<3){s.KeyPreset="Hush";s.MousePreset="Felt";s.Release=false;s.Scroll=false;s.Stereo=false;s.KeyVolume=60;s.MouseVolume=65;s.SoundRevision=3;}
            s.KeyVolume = Math.Clamp(s.KeyVolume,0,100); s.MouseVolume = Math.Clamp(s.MouseVolume,0,100);
            s.ScrollVolume = Math.Clamp(s.ScrollVolume,0,100); s.ReleaseVolume = Math.Clamp(s.ReleaseVolume,0,100);
            if (!SoundBank.Names.Contains(s.KeyPreset) && s.KeyPreset != "Custom") s.KeyPreset="Hush";
            if (!SoundBank.Names.Contains(s.MousePreset) && s.MousePreset != "Custom") s.MousePreset="Felt";
            return s;
        } catch { return new(){SoundRevision=3}; }
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder); string temp=FileName+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temp,FileName,true);
    }
    public static bool StartupEnabled()
    {
        using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("ClicksPersonal") != null;
    }
    public static void SetStartup(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("ClicksPersonal","\""+Environment.ProcessPath+"\" --tray");
        else key.DeleteValue("ClicksPersonal",false);
    }
}
