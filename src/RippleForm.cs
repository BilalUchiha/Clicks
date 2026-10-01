using System.Drawing.Drawing2D;
namespace Clicks;
public sealed class RippleForm:Form
{
    readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    readonly long started=Environment.TickCount64;readonly Color color;
    public RippleForm(int x,int y,bool right)
    {
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
        BackColor=Color.Magenta;TransparencyKey=Color.Magenta;DoubleBuffered=true;
        AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.Manual;
        Bounds=new Rectangle(x-45,y-45,90,90);color=right?Color.FromArgb(251,191,36):Color.FromArgb(129,140,248);
        timer.Tick+=(_,_)=>{if(Environment.TickCount64-started>320)Close();else Invalidate();};timer.Start();
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x20|0x80000|0x8000000|0x80;return p;}}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);double age=Math.Clamp((Environment.TickCount64-started)/320d,0,1);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        int size=(int)(20+48*age);using var pen=new Pen(color,3);
        e.Graphics.DrawEllipse(pen,(90-size)/2,(90-size)/2,size,size);Opacity=Math.Max(.05,1-age);
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
