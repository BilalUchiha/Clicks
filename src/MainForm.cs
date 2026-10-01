using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Collections.Concurrent;
using Microsoft.Win32;

namespace Clicks;

public sealed class MainForm : Form
{
    static readonly Color Bg=Color.FromArgb(15,23,42),Card=Color.FromArgb(25,33,52),Muted=Color.FromArgb(180,191,209),Accent=Color.FromArgb(165,180,252);
    readonly Settings settings=Settings.Load();
    readonly AudioEngine audio;
    readonly SoundController sounds;
    readonly InputHooks hooks;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer tick=new(){Interval=250},saveTimer=new(){Interval=500};
    readonly System.Windows.Forms.Timer rippleTimer=new(){Interval=16};
    readonly ConcurrentQueue<InputEvent> rippleQueue=new();
    readonly FlowLayoutPanel content=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(24)};
    readonly Label status=new(),counts=new();
    readonly Button power=new();
    readonly Label shortcutHelp=new();
    ToolStripItem? muteMenu;
    readonly bool startHidden,render;
    bool quitting,hotkeyRegistered,saveWarning;
    long lastForegroundCheck;
    string foreground="";
    string shortcut="Ctrl+Alt+M";
    public MainForm(bool startHidden=false,bool render=false)
    {
        this.startHidden=startHidden;this.render=render;
        Text="Clicks · Make your computer feel good";
        Font=new Font("Segoe UI",10);BackColor=Bg;ForeColor=Color.White;
        ClientSize=new Size(660,790);MinimumSize=new Size(620,480);StartPosition=FormStartPosition.CenterScreen;
        AutoScaleMode=AutoScaleMode.Dpi;
        Icon=MakeIcon();
        SoundBank.Initialize();audio=new AudioEngine(!render);sounds=new SoundController(settings,audio);
        LoadCustom(true);LoadCustom(false);
        hooks=new InputHooks();hooks.Input+=OnInput;
        Controls.Add(content);
        BuildUI();
        var menu=new ContextMenuStrip();
        menu.Items.Add("Open Clicks",null,(_,_)=>OpenWindow());
        muteMenu=menu.Items.Add("Mute / unmute  (Ctrl+Alt+M)",null,(_,_)=>TogglePower());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit Clicks",null,(_,_)=>Quit());
        tray=new NotifyIcon{Icon=Icon,Text="Clicks · Sound on",ContextMenuStrip=menu,Visible=!render};
        tray.DoubleClick+=(_,_)=>OpenWindow();
        saveTimer.Tick+=(_,_)=>{saveTimer.Stop();Save();};
        tick.Tick+=(_,_)=>UpdateState();tick.Start();
        rippleTimer.Tick+=(_,_)=>ShowPendingRipples();rippleTimer.Enabled=settings.Ripples;
        SystemEvents.PowerModeChanged+=OnPowerMode;
        Shown+=(_,_)=>
        {
            hotkeyRegistered=RegisterHotKey(Handle,71,0x4000|1|2,0x4D);
            if(!hotkeyRegistered){shortcut="Ctrl+Alt+F10";hotkeyRegistered=RegisterHotKey(Handle,71,0x4000|1|2,0x79);}
            shortcutHelp.Text=(hotkeyRegistered?shortcut+" to mute":"Use the tray menu to mute")+" • Close the window to keep sounds running.\nOffline. No accounts. No typed text recorded or sent.";
            muteMenu.Text="Mute / unmute"+(hotkeyRegistered?"  ("+shortcut+")":"");
            UpdateState();
            if(render)
            {
                using var image=new Bitmap(Width,Height);
                // Capture our own app's rendered control tree for layout verification.
                DrawToBitmap(image,new Rectangle(Point.Empty,Size));
                image.Save(Path.Combine(AppContext.BaseDirectory,"preview.png"));
                content.AutoScrollPosition=new Point(0,1200);
                DrawToBitmap(image,new Rectangle(Point.Empty,Size));
                image.Save(Path.Combine(AppContext.BaseDirectory,"preview-bottom.png"));
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ui-check.json"),System.Text.Json.JsonSerializer.Serialize(new{hotkeyRegistered,shortcut,keyboard=settings.KeyPreset,mouse=settings.MousePreset}));
                quitting=true;Close();
            }
            else if(startHidden) Hide();
        };
        FormClosing+=(_,e)=>{if(!quitting){e.Cancel=true;Hide();}};
    }
    void BuildUI()
    {
        var header=new Panel{Width=580,Height=120,Margin=new Padding(0,0,0,16)};
        header.Controls.Add(LabelAt("Clicks",0,0,380,60,24,Color.White,FontStyle.Bold));
        header.Controls.Add(LabelAt("A little joy in every keystroke and click.",2,61,560,26,11,Muted));
        status.SetBounds(2,92,570,24);status.ForeColor=Accent;header.Controls.Add(status);
        power.SetBounds(435,12,145,44);StyleButton(power);power.Click+=(_,_)=>TogglePower();header.Controls.Add(power);
        content.Controls.Add(header);
        AddSoundCard(true);AddSoundCard(false);
        var details=Section("Make it yours",270);
        AddCheck(details,"Play release sounds",16,49,settings.Release,v=>settings.Release=v);
        AddCheck(details,"Natural sound variation",300,49,settings.Variation,v=>settings.Variation=v);
        AddCheck(details,"Stereo positioning",16,86,settings.Stereo,v=>settings.Stereo=v);
        AddCheck(details,"Show click ripples",300,86,settings.Ripples,v=>settings.Ripples=v);
        AddCheck(details,"Scroll ticks",16,123,settings.Scroll,v=>settings.Scroll=v);
        details.Controls.Add(LabelAt("Scroll level",16,166,120,24,10,Muted));
        AddSlider(details,145,159,240,settings.ScrollVolume,v=>settings.ScrollVolume=v);
        details.Controls.Add(LabelAt("Release level",16,217,120,24,10,Muted));
        AddSlider(details,145,210,240,settings.ReleaseVolume,v=>settings.ReleaseVolume=v);
        var options=Section("Keep it comfortable",214);
        var startup=AddCheck(options,"Start with Windows",16,49,Settings.StartupEnabled(),v=>
        {
            try{Settings.SetStartup(v);}catch(Exception e){MessageBox.Show(e.Message,"Couldn't change startup");}
        });
        options.Controls.Add(LabelAt("Quiet in these apps (process names, separated by commas)",16,92,550,25,10,Muted));
        var excluded=new TextBox{Text=settings.MutedApps,Left=16,Top=124,Width=548,BackColor=Bg,ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,AccessibleName="Muted application process names"};
        excluded.TextChanged+=(_,_)=>{settings.MutedApps=excluded.Text;Changed();};options.Controls.Add(excluded);
        options.Controls.Add(LabelAt("Example: Teams, Zoom, Discord. Applies while that app is focused.",16,161,548,37,9,Muted));
        var footer=Section("Try it anywhere",160);
        footer.Controls.Add(LabelAt("Type in any app or click around your desktop. No text is saved.",16,47,548,28,10,Muted));
        counts.SetBounds(16,82,548,28);counts.ForeColor=Accent;footer.Controls.Add(counts);
        var reconnect=ButtonAt(footer,"Reconnect audio",16,116,180,32);reconnect.Click+=(_,_)=>Reconnect();
        var hide=ButtonAt(footer,"Hide to tray",210,116,155,32);hide.Click+=(_,_)=>Hide();
        var quit=ButtonAt(footer,"Quit",379,116,100,32);quit.Click+=(_,_)=>Quit();
        shortcutHelp.Width=580;shortcutHelp.Height=60;shortcutHelp.ForeColor=Muted;shortcutHelp.Margin=new Padding(0,2,0,12);content.Controls.Add(shortcutHelp);
        content.Resize+=(_,_)=>ResizeContent();ResizeContent();
    }
    void ResizeContent()
    {
        int width=Math.Max(560,content.ClientSize.Width-content.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-2);
        foreach(Control c in content.Controls)c.Width=width;
    }
    Panel Section(string title,int height)
    {
        var p=new Panel{Width=580,Height=height,BackColor=Card,Margin=new Padding(0,0,0,16)};
        p.Controls.Add(LabelAt(title,16,13,540,30,14,Color.White,FontStyle.Bold));content.Controls.Add(p);return p;
    }
    void AddSoundCard(bool keyboard)
    {
        var p=Section(keyboard?"Keyboard":"Mouse",185);
        var enabled=AddCheck(p,"Enabled",450,16,keyboard?settings.Keyboard:settings.Mouse,v=>{if(keyboard)settings.Keyboard=v;else settings.Mouse=v;});
        enabled.Width=110;
        var picker=ButtonAt(p,"",16,53,220,35);
        var presets=new ContextMenuStrip();
        picker.AccessibleName=keyboard?"Choose keyboard sound preset":"Choose mouse sound preset";
        picker.Click+=(_,_)=>presets.Show(picker,new Point(0,picker.Height));
        var description=LabelAt("",16,95,540,24,10,Muted);p.Controls.Add(description);
        void SetDescription(){string current=keyboard?settings.KeyPreset:settings.MousePreset;picker.Text=current+"  ▾";int index=Array.IndexOf(SoundBank.Names,current);description.Text=index>=0?SoundBank.Descriptions[index]:"Your own imported sound. Choose a WAV or MP3 file below.";foreach(ToolStripMenuItem item in presets.Items)item.Checked=item.Text==current;}
        SetDescription();
        foreach(string name in SoundBank.Names.Append("Custom"))
        {
            var item=new ToolStripMenuItem(name);presets.Items.Add(item);
            item.Click+=(_,_)=>
            {
                if(name=="Custom"&&(keyboard?sounds.CustomKey:sounds.CustomMouse)==null&&!Import(keyboard))return;
                if(keyboard)settings.KeyPreset=name;else settings.MousePreset=name;
                SetDescription();Changed();Preview(keyboard);
            };
        }
        SetDescription();picker.Disposed+=(_,_)=>presets.Dispose();
        var preview=ButtonAt(p,"Preview",250,53,124,35);preview.Click+=(_,_)=>Preview(keyboard);
        var import=ButtonAt(p,"Import sound…",388,53,172,35);import.Click+=(_,_)=>{if(Import(keyboard)){SetDescription();Changed();Preview(keyboard);}};
        p.Controls.Add(LabelAt("Volume",16,140,90,25,10,Muted));
        AddSlider(p,100,130,335,keyboard?settings.KeyVolume:settings.MouseVolume,v=>{if(keyboard)settings.KeyVolume=v;else settings.MouseVolume=v;});
    }
    static Label LabelAt(string text,int x,int y,int w,int h,float size,Color color,FontStyle style=FontStyle.Regular)=>new(){Text=text,Left=x,Top=y,Width=w,Height=h,Font=new Font("Segoe UI",size,style),ForeColor=color};
    static void StyleButton(Button b)
    {
        b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(100,116,139);b.FlatAppearance.MouseOverBackColor=Color.FromArgb(51,65,85);
        b.BackColor=Color.FromArgb(38,49,71);b.ForeColor=Color.White;b.Cursor=Cursors.Hand;
    }
    static Button ButtonAt(Panel p,string text,int x,int y,int w,int h)
    {var b=new Button{Text=text,Left=x,Top=y,Width=w,Height=h};StyleButton(b);p.Controls.Add(b);return b;}
    CheckBox AddCheck(Panel p,string text,int x,int y,bool initial,Action<bool> set)
    {
        var box=new CheckBox{Text=text,Left=x,Top=y,Width=265,Height=30,Checked=initial,ForeColor=Color.White,Cursor=Cursors.Hand};
        box.CheckedChanged+=(_,_)=>{set(box.Checked);Changed();};p.Controls.Add(box);return box;
    }
    void AddSlider(Panel p,int x,int y,int width,int initial,Action<int> set)
    {
        var slider=new TrackBar{Left=x,Top=y,Width=width,Height=40,Minimum=0,Maximum=100,TickStyle=TickStyle.None,Value=initial,SmallChange=1,LargeChange=10,BackColor=Card,AccessibleName="Volume percentage"};
        var value=LabelAt(initial+"%",x+width+8,y+8,70,24,10,Accent);
        slider.ValueChanged+=(_,_)=>{set(slider.Value);value.Text=slider.Value+"%";Changed();};p.Controls.Add(slider);p.Controls.Add(value);
    }
    void OnInput(InputEvent e)
    {
        if(render)return;
        // Called on the dedicated hook thread: no file writes, text decoding, or network activity.
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(),out uint foregroundPid);
            // Preview and preset buttons already play their own tap; avoid doubling it.
            if(foregroundPid==(uint)Environment.ProcessId)return;
            bool played=sounds.Handle(e);
            if(played&&settings.Ripples&&!e.Keyboard&&!e.Scroll&&!e.Up&&rippleQueue.Count<12)rippleQueue.Enqueue(e);
        }
        catch{ /* Always let Windows deliver the original input. */ }
    }
    void UpdateState()
    {
        power.Text=settings.Enabled?"Mute sounds":"Enable sounds";
        power.ForeColor=settings.Enabled?Accent:Color.White;
        if(tray!=null)tray.Text=settings.Enabled?"Clicks · Sound on":"Clicks · Muted";
        if(Environment.TickCount64-lastForegroundCheck>500)
        {
            lastForegroundCheck=Environment.TickCount64;
            GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);
            try{using var process=Process.GetProcessById((int)pid);foreground=process.ProcessName;}catch{foreground="";}
            sounds.AppMuted=(settings.MutedApps??"").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Any(x=>Path.GetFileNameWithoutExtension(x).Equals(foreground,StringComparison.OrdinalIgnoreCase));
            if(sounds.AppMuted)audio.Clear();
        }
        status.Text=audio.Error!=null?"Audio disconnected. Use Reconnect audio below.":!settings.Enabled?"Muted · "+(hotkeyRegistered?shortcut+" to enable":"Use the tray menu to enable"):sounds.AppMuted?"Quiet in "+foreground:"Listening across Windows · "+(hotkeyRegistered?shortcut+" to mute":"Shortcut unavailable; use the tray menu");
        counts.Text=$"This session: {sounds.Keys:N0} key presses · {sounds.Clicks:N0} clicks · {sounds.Scrolls:N0} scroll ticks";
    }
    void ShowPendingRipples()
    {
        while(rippleQueue.TryDequeue(out var e))
            if(settings.Enabled&&settings.Ripples&&!sounds.AppMuted)new RippleForm(e.X,e.Y,e.Code==1).Show();
    }
    void TogglePower(){settings.Enabled=!settings.Enabled;if(!settings.Enabled)audio.Clear();Changed();UpdateState();}
    void Changed(){saveTimer.Stop();saveTimer.Start();rippleTimer.Enabled=settings.Ripples;if(!settings.Ripples)while(rippleQueue.TryDequeue(out _)){} }
    void Save()
    {
        if(render)return;
        try{settings.Save();}catch(Exception e){if(!saveWarning){saveWarning=true;MessageBox.Show("Settings could not be saved.\n"+e.Message,"Clicks");}}
    }
    void Preview(bool keyboard)
    {
        string preset=keyboard?settings.KeyPreset:settings.MousePreset;
        float[]? custom=keyboard?sounds.CustomKey:sounds.CustomMouse;
        float volume=(keyboard?settings.KeyVolume:settings.MouseVolume)/100f;
        if(preset=="Gunshot"){audio.Play(SoundBank.Gunshot,volume,fullLength:true,stereo:true);return;}
        audio.Play(preset=="Custom"&&custom!=null?custom:SoundBank.Get(preset=="Custom"?"Hush":preset,0,false,2),volume);
        var timer=new System.Windows.Forms.Timer{Interval=85};
        timer.Tick+=(_,_)=>{timer.Stop();timer.Dispose();if(settings.Release)audio.Play(preset=="Custom"&&custom!=null?custom:SoundBank.Get(preset=="Custom"?"Hush":preset,0,true,2),volume*settings.ReleaseVolume/100f);};timer.Start();
    }
    bool Import(bool keyboard)
    {
        using var dialog=new OpenFileDialog{Title="Choose a short click sound",Filter="Sound files (*.wav;*.mp3)|*.wav;*.mp3",CheckFileExists=true};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return false;
        try
        {
            float[] data=SoundBank.LoadCustom(dialog.FileName);
            Directory.CreateDirectory(Settings.Folder);
            string target=Path.Combine(Settings.Folder,(keyboard?"keyboard":"mouse")+Path.GetExtension(dialog.FileName).ToLowerInvariant());
            if(!Path.GetFullPath(dialog.FileName).Equals(target,StringComparison.OrdinalIgnoreCase))File.Copy(dialog.FileName,target,true);
            if(keyboard){settings.CustomKey=target;sounds.CustomKey=data;settings.KeyPreset="Custom";}
            else{settings.CustomMouse=target;sounds.CustomMouse=data;settings.MousePreset="Custom";}
            return true;
        }
        catch(Exception e){MessageBox.Show(e.Message,"Couldn't import sound",MessageBoxButtons.OK,MessageBoxIcon.Information);return false;}
    }
    void LoadCustom(bool keyboard)
    {
        string path=keyboard?settings.CustomKey:settings.CustomMouse;
        if(string.IsNullOrEmpty(path))return;
        try{if(keyboard)sounds.CustomKey=SoundBank.LoadCustom(path);else sounds.CustomMouse=SoundBank.LoadCustom(path);}
        catch{if(keyboard)settings.KeyPreset="Hush";else settings.MousePreset="Felt";}
    }
    void Reconnect(){try{audio.Start();}catch(Exception e){MessageBox.Show(e.Message,"Couldn't reconnect audio");}}
    void OnPowerMode(object sender,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Resume&&!IsDisposed)BeginInvoke(()=>Reconnect());}
    void OpenWindow(){Show();WindowState=FormWindowState.Normal;Activate();}
    void Quit(){quitting=true;Close();}
    protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam==71){TogglePower();return;}base.WndProc(ref m);}
    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            SystemEvents.PowerModeChanged-=OnPowerMode;
            if(hotkeyRegistered)UnregisterHotKey(Handle,71);
            tick.Dispose();saveTimer.Dispose();rippleTimer.Dispose();hooks.Dispose();audio.Dispose();tray.Visible=false;tray.Dispose();Save();
        }
        base.Dispose(disposing);
    }
    static Icon MakeIcon()
    {
        using var b=new Bitmap(32,32);using(var g=Graphics.FromImage(b))
        {g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);using var brush=new SolidBrush(Color.FromArgb(67,56,202));g.FillEllipse(brush,1,1,30,30);using var pen=new Pen(Color.White,2.8f);g.DrawArc(pen,8,8,16,16,65,230);g.DrawLine(pen,18,18,24,24);}
        nint h=b.GetHicon();using var temporary=Icon.FromHandle(h);var result=(Icon)temporary.Clone();DestroyIcon(h);return result;
    }
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint window,int id);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window,out uint id);
    [DllImport("user32.dll")] static extern bool DestroyIcon(nint icon);
}
