using System;
using System.Threading;
using Godot;
using MinorShift.Emuera;

public class EmueraThread
{
    public static EmueraThread instance { get { return instance_; } }
    static readonly EmueraThread instance_ = new EmueraThread();

    EmueraThread() { }

    public void Start(bool debug)
    {
        if (thread != null && thread.IsAlive)
        {
            GD.PushWarning("[EmueraThread] Already running — ignoring duplicate Start()");
            return;
        }
        debugmode = debug;
        running = true;
        thread = new Thread(Work) { IsBackground = true, Name = "EmueraWorker" };
        thread.Start();
    }

    /// <summary>running=false 후 스레드 완전 종료까지 대기 (최대 3초)</summary>
    public void End()
    {
        running = false;
        var t = thread;
        if (t != null && t.IsAlive)
        {
            if (!t.Join(TimeSpan.FromSeconds(3)))
                GD.PushWarning("[EmueraThread] Worker did not stop in time");
        }
        thread = null;
    }

    public bool Running()
    {
        var console = GlobalStatic.Console;
        return console != null && console.IsInProcess;
    }

    public void Input(string c, bool fromButton, bool skip = false)
    {
        var console = GlobalStatic.Console;
        if (console == null) return;
        if (!fromButton && console.IsWaitingInputSomething) return;
        Volatile.Write(ref input, c);
        skipflag = skip;
    }

    public bool IsSkipFlag => skipflag;

    void Work()
    {
        Program.debugMode = debugmode;
        Program.Main(Array.Empty<string>());

        uEmuera.Utils.ResourceClear();
        GC.Collect();

        string? localInput = null;
        var console = GlobalStatic.Console;
        if (console == null) return;

        while (running)
        {
            skipflag = false;

            // 입력 대기
            while ((localInput = Volatile.Read(ref input)) == null)
            {
                Thread.Sleep(1);
                if (!running) return;
                uEmuera.Forms.Timer.Update();
            }

            if (console.IsWaitingInput)
            {
                if (console.IsWaitingEnterKey)
                    localInput = "";
                console.PressEnterKey(skipflag, localInput, false);
            }
            Thread.Sleep(10);
            Volatile.Write(ref input, null);
        }
    }

    Thread? thread;
    bool debugmode;
    volatile bool running;
    string? input;
    volatile bool skipflag;
}
