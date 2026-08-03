using System;
using System.Threading;
using Godot;
using MinorShift.Emuera;

public class EmueraThread
{
    public static EmueraThread instance { get { return instance_; } }
    static readonly EmueraThread instance_ = new EmueraThread();

    EmueraThread() { }

    /// <summary>
    /// Emuera 엔진 스레드를 시작한다. 이미 동작 중이면 아무것도 하지 않는다.
    /// 다중 실행을 허용하면 동일한 static GlobalStatic.Console 을 두 엔진이
    /// 공유해 상태가 깨진다.
    /// </summary>
    public bool Start(bool debug)
    {
        lock (sync)
        {
            if (thread != null && thread.IsAlive)
            {
                uEmuera.Logger.Warn("EmueraThread.Start: already running");
                return false;
            }
            debugmode = debug;
            running = true;
            thread = new Thread(Work) { IsBackground = true, Name = "EmueraEngine" };
            thread.Start();
            return true;
        }
    }

    /// <summary>
    /// 스레드 정지를 요청하고 실제로 종료될 때까지 기다린다.
    /// Join 없이 다음 Start() 를 호출하면 엔진이 두 번 돈다.
    /// </summary>
    public void End(int timeoutMs = 3000)
    {
        Thread? t;
        lock (sync)
        {
            running = false;
            t = thread;
            thread = null;
        }
        if (t != null && t.IsAlive)
        {
            if (!t.Join(timeoutMs))
                uEmuera.Logger.Warn($"EmueraThread.End: thread did not exit within {timeoutMs}ms");
        }
        input = null;
        skipflag = false;
    }

    public bool Running()
    {
        var console = GlobalStatic.Console;
        return console != null && console.IsInProcess;
    }

    public bool IsAlive
    {
        get { lock (sync) return thread != null && thread.IsAlive; }
    }

    public void Input(string c, bool fromButton, bool skip = false)
    {
        var console = GlobalStatic.Console;
        if (console == null) return;
        if (!fromButton && console.IsWaitingInputSomething) return;
        skipflag = skip;
        input = c;
    }

    public bool IsSkipFlag => skipflag;

    void Work()
    {
        try
        {
            Program.debugMode = debugmode;
            // uEmuera.Application.Run() 은 win.Init() 을 호출하고 즉시 반환한다
            // (WinForms처럼 블로킹하지 않는다). 즉 이 줄 직후에
            // 콘솔은 초기화 완료·게임은 로드 완료 상태가 된다.
            // 이전에는 여기서 ResourceClear()+GC.Collect() 를 호출했는데,
            // 그건 '로드한 직후에 해제한다'는 잘못된 동작이었다.
            Program.Main(Array.Empty<string>());

            input = null;
            while (running)
            {
                skipflag = false;
                while (input == null)
                {
                    Thread.Sleep(1);
                    if (!running) return;
                    uEmuera.Forms.Timer.Update();
                }

                // console 은 Emuera 초기화나 Clear() 로 교체되므로 매번 다시 가져온다.
                // 루프 밖에서 한 번만 가져오면 NullReferenceException 이 된다.
                var console = GlobalStatic.Console;
                if (console != null && console.IsWaitingInput)
                {
                    var value = input;
                    if (console.IsWaitingEnterKey)
                        value = "";
                    console.PressEnterKey(skipflag, value, false);
                }
                Thread.Sleep(10);
                input = null;
            }
        }
        catch (Exception e)
        {
            // 백그라운드 스레드의 미처리 예외는 프로세스를 종료시키므로 반드시 잡는다
            uEmuera.Logger.Error($"EmueraThread crashed: {e}");
        }
        finally
        {
            running = false;
        }
    }

    readonly object sync = new object();
    Thread? thread;
    bool debugmode;
    volatile bool running;
    volatile string? input;
    volatile bool skipflag;
}
