using Godot;
using System;
using System.Threading;
using MinorShift.Emuera;

public partial class EmueraMain : Node
{
    bool working;
    bool pendingClear;
    bool pendingRestart;

    EmueraContent? content;

    public override void _Ready()
    {
        uEmuera.Logger.info = GD.Print;
        uEmuera.Logger.warn = s => GD.PushWarning(s?.ToString() ?? "");
        uEmuera.Logger.error = s => GD.PushError(s?.ToString() ?? "");

        // 화면 크기 캐시 (EmueraConsole.ClientWidth/Height 스레드 안전 접근용)
        GameState.UpdateScreenSize(DisplayServer.WindowGetSize());

        content = GetNode<EmueraContent>("EmueraContent");
        GenericUtils.SetContent(content);

        // Config.SetConfig 이후 DrawableWidth를 실제 화면 폭으로 override
        // → ConfigPostProcess에서 처리됨
        CallDeferred(MethodName.StartGame);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWmSizeChanged)
            GameState.UpdateScreenSize(DisplayServer.WindowGetSize());
    }

    void StartGame()
    {
        string gamePath = GameState.SelectedGamePath;
        if (string.IsNullOrEmpty(gamePath))
        {
            // 직접 실행 시 fallback
            gamePath = GetDefaultGamePath();
        }
        MinorShift._Library.Sys.ExeDir = gamePath;
        GD.Print("[EmueraMain] Starting game at: " + gamePath);

        EmueraThread.instance.Start(false);
        working = true;
    }

    static string GetDefaultGamePath()
    {
#if GODOT_ANDROID
        string p = "/storage/emulated/0/emuera/";
        if (!System.IO.Directory.Exists(p))
            p = OS.GetUserDataDir() + "/emuera/";
        return p;
#else
        string p = OS.GetExecutablePath().GetBaseDir().PathJoin("emuera") + "/";
        if (!System.IO.Directory.Exists(p))
            p = OS.GetUserDataDir() + "/emuera/";
        return p;
#endif
    }

    public void RequestClear()  => pendingClear = true;
    public void RequestRestart() => pendingRestart = true;

    public override void _Process(double delta)
    {
        if (pendingClear)   { pendingClear = false;   DoClear(); }
        if (pendingRestart) { pendingRestart = false; DoRestart(); }

        // 워커 스레드에서 요청된 텍스처를 메인 스레드에서 생성
        SpriteManager.ProcessPendingTextures();
    }

    void DoClear()
    {
        // 먼저 스레드 중지 (Join으로 완전 종료 대기)
        EmueraThread.instance.End();

        content?.Clear();

        var console = GlobalStatic.Console;
        console?.ClearDisplay();
        console?.Dispose();
        MinorShift.Emuera.Content.AppContents.UnloadContents();
        ConfigData.Instance.Clear();
        SpriteManager.ForceClear();
        GC.Collect();
        working = false;

        // 게임 선택 화면으로 돌아감
        GetTree().ChangeSceneToFile("res://first_window.tscn");
    }

    void DoRestart()
    {
        EmueraThread.instance.End();
        content?.Clear();
        var console = GlobalStatic.Console;
        console?.ClearDisplay();
        console?.Dispose();
        MinorShift.Emuera.Content.AppContents.UnloadContents();
        ConfigData.Instance.Clear();
        SpriteManager.ForceClear();
        GC.Collect();

        // 같은 씬을 재시작 (game path는 GameState에 남아있음)
        CallDeferred(MethodName.StartGame);
    }

    public bool IsWorking => working;
}
