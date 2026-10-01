namespace Clicks;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--test")) { Diagnostics.Run(); return; }
        using var single = new Mutex(true, "Local\\Clicks.Personal.SoundApp", out bool first);
        if (!first) { MessageBox.Show("Clicks is already running. Open it from the system tray.", "Clicks"); return; }
        try { Application.Run(new MainForm(args.Contains("--tray"), args.Contains("--render"))); }
        catch (Exception e) { MessageBox.Show("Clicks couldn't start. Check that an audio output is connected.\n\n" + e.Message, "Clicks", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
