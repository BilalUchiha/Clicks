using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Clicks;
public readonly record struct InputEvent(bool Keyboard,int Code,bool Up,bool Scroll,int X=0,int Y=0);
public sealed class InputHooks:IDisposable
{
    delegate nint HookProc(int code,nint wParam,nint lParam);
    readonly HookProc keyboardProc,mouseProc; nint keyboardHook,mouseHook;
    readonly Thread thread;
    uint threadId;
    readonly HashSet<int> down=[];
    public event Action<InputEvent>? Input;
    public bool Installed=>keyboardHook!=0&&mouseHook!=0;
    public InputHooks()
    {
        keyboardProc=KeyboardCallback;mouseProc=MouseCallback;
        using var ready=new ManualResetEventSlim();Exception? failure=null;
        // Keep hook callbacks independent of settings dialogs, imports, and UI painting.
        thread=new Thread(()=>
        {
            threadId=GetCurrentThreadId();
            try
            {
                keyboardHook=SetWindowsHookEx(13,keyboardProc,GetModuleHandle(null),0);
                mouseHook=SetWindowsHookEx(14,mouseProc,GetModuleHandle(null),0);
                if(!Installed)throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not listen for keyboard and mouse events.");
            }
            catch(Exception e){failure=e;}
            ready.Set();
            try{if(failure==null)Application.Run();}
            finally
            {
                if(keyboardHook!=0)UnhookWindowsHookEx(keyboardHook);
                if(mouseHook!=0)UnhookWindowsHookEx(mouseHook);
                keyboardHook=mouseHook=0;
            }
        }){IsBackground=true,Name="Clicks input events"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();ready.Wait();
        if(failure!=null){thread.Join();throw failure;}
    }
    nint KeyboardCallback(int code,nint wParam,nint lParam)
    {
        if(code>=0)
        {
            var data=Marshal.PtrToStructure<KeyboardData>(lParam);
            if((data.Flags&0x10)==0)
            {
                int vk=(int)data.Key;bool up=(int)wParam is 0x101 or 0x105,press=(int)wParam is 0x100 or 0x104;
                if((up&&down.Remove(vk))||(press&&down.Add(vk))) Input?.Invoke(new(true,vk,up,false));
            }
        }
        return CallNextHookEx(keyboardHook,code,wParam,lParam);
    }
    nint MouseCallback(int code,nint wParam,nint lParam)
    {
        if(code>=0)
        {
            var data=Marshal.PtrToStructure<MouseData>(lParam);int message=(int)wParam;
            if((data.Flags&1)==0 && message is 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20B or 0x20C or 0x20A or 0x20E)
            {
                bool up=message is 0x202 or 0x205 or 0x208 or 0x20C,scroll=message is 0x20A or 0x20E;
                int button=message switch{0x204 or 0x205=>1,0x207 or 0x208=>2,0x20B or 0x20C=>3,_=>0};
                Input?.Invoke(new(false,button,up,scroll,data.X,data.Y));
            }
        }
        return CallNextHookEx(mouseHook,code,wParam,lParam);
    }
    public void Dispose()
    {if(thread.IsAlive){PostThreadMessage(threadId,0x12,0,0);thread.Join(2000);}}
    [StructLayout(LayoutKind.Sequential)] struct KeyboardData {public uint Key,Scan,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)] struct MouseData {public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [DllImport("user32.dll",SetLastError=true)] static extern nint SetWindowsHookEx(int hook,HookProc proc,nint module,uint thread);
    [DllImport("user32.dll")] static extern nint CallNextHookEx(nint hook,int code,nint wParam,nint lParam);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern nint GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,nuint wParam,nint lParam);
}
