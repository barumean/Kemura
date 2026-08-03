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
    /// Emueraエンジンスレッドを開始する。既に動作中の場合は何もしない。
    /// 多重起動を許すと同一のstatic GlobalStatic.Consoleを2つのエンジンが
    /// 共有して状態が壊れる。
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
    /// スレッドの停止を要求し、実際に終了するまで待つ。
    /// Joinせずに次のStart()を呼ぶとエンジンが二重に走る。
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
            // uEmuera.Application.Run()はwin.Init()を呼んで即座に戻る
            // (WinFormsのようにブロックしない)。つまりこの行の直後で
            // コンソールは初期化済み・ゲームはロード済みになっている。
            // 以前はここでResourceClear()+GC.Collect()を呼んでいたが、
            // それは「ロードした直後に解放する」という誤りだった。
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

                // consoleはEmuera初期化やClear()で差し替わるため毎回取り直す。
                // ループ外で1度だけ取得するとNullReferenceExceptionになる。
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
            // バックグラウンドスレッドの未処理例外はプロセスを落とすため必ず捕える
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
