using System;
using System.Threading;
using Godot;
using MinorShift.Emuera;

public class EmueraThread
{
    public static EmueraThread instance { get { return instance_; } }
    static readonly EmueraThread instance_ = new EmueraThread();

    EmueraThread() { }

    public void Start(bool debug, bool useCoroutine)
    {
        debugmode = debug;
        running = true;
        thread = new Thread(Work) { IsBackground = true };
        thread.Start();
    }

    public void End()
    {
        running = false;
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
        input = c;
        skipflag = skip;
    }

    public bool IsSkipFlag => skipflag;

    void Work()
    {
        Program.debugMode = debugmode;
        Program.Main(Array.Empty<string>());

        uEmuera.Utils.ResourceClear();
        GC.Collect();

        input = null;
        var console = GlobalStatic.Console;
        while (running)
        {
            skipflag = false;
            while (input == null)
            {
                Thread.Sleep(1);
                if (!running) return;
                uEmuera.Forms.Timer.Update();
            }
            if (console.IsWaitingInput)
            {
                if (console.IsWaitingEnterKey)
                    input = "";
                console.PressEnterKey(skipflag, input, false);
            }
            Thread.Sleep(10);
            input = null;
        }
    }

    Thread? thread;
    bool debugmode;
    bool running;
    string? input;
    bool skipflag;
}
