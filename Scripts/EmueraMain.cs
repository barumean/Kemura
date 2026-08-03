using Godot;
using System;
using MinorShift.Emuera;

/// <summary>
/// 게임 본체 제어. main.tscn 의 Main/EmueraMain 에 붙는다.
///
/// 시작 시 게임을 바로 시작하면 안 된다(먼저 FirstWindow에서 게임을 선택하게 한다).
/// 게임 시작은 FirstWindow에서 StartGame()을 호출해 이뤄진다.
/// </summary>
public partial class EmueraMain : Node
{
    bool working;
    bool pendingClear;
    bool pendingRestart;

    EmueraContent? content;
    FirstWindow? firstWindow;

    public override void _Ready()
    {
        // Logger를 가장 먼저 연결한다(이후 로그를 놓치지 않기 위해)
        // GD.Print 는 Print(params object[]) 이므로 Action<object> 에
        // 메서드 그룹 그대로 대입할 수 없다 (CS0123). 람다로 감싼다.
        uEmuera.Logger.info = s => GD.Print(s?.ToString() ?? "");
        uEmuera.Logger.warn = s => GD.PushWarning(s?.ToString() ?? "");
        uEmuera.Logger.error = s => GD.PushError(s?.ToString() ?? "");

        // SHIFT-JIS로 만든 emuera.config 의 키 이름을 해석하기 위한 변환 테이블.
        // 게임 로드보다 먼저 넣어둬야 한다.
        ConfigMaps.Load();

        // 절대 경로("/root/Main/...")를 박으면 이 씬을 자식으로
        // 인스턴스화하는 순간 깨진다. 형제 노드는 상대 경로로 참조한다.
        content = GetNodeOrNull<EmueraContent>("../EmueraContent");
        firstWindow = GetNodeOrNull<FirstWindow>("../FirstWindow");

        if (content == null)
            GD.PushError("EmueraMain: EmueraContent 를 찾을 수 없습니다 (../EmueraContent)");
        if (firstWindow == null)
            GD.PushError("EmueraMain: FirstWindow 를 찾을 수 없습니다 (../FirstWindow)");

        GenericUtils.SetContent(content);

        // 시작 직후에는 게임 선택 화면만 보여준다
        content?.Hide();
        firstWindow?.Show();
    }

    /// <summary>FirstWindow에서 호출된다. gamePath는 게임 폴더의 절대 경로.</summary>
    internal bool StartGame(string gamePath)
    {
        if (working)
        {
            GD.PushWarning("EmueraMain.StartGame: already working");
            return false;
        }
        if (string.IsNullOrEmpty(gamePath))
            return false;

        // Sys.ExeDir 는 private set 이라 직접 대입할 수 없다(CS0272).
        // 올바른 진입점은 SetWorkFolder(부모) + SetSourceFolder(폴더명) 이며,
        // 이 조합이 ExeDir 를 정규화해 조립한다.
        var full = gamePath.TrimEnd('/', '\\');
        var parent = System.IO.Path.GetDirectoryName(full);
        var name = System.IO.Path.GetFileName(full);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            GD.PushError($"EmueraMain.StartGame: 잘못된 게임 경로 '{gamePath}'");
            return false;
        }
        MinorShift._Library.Sys.SetWorkFolder(parent);
        MinorShift._Library.Sys.SetSourceFolder(name);
        currentGamePath = full;
        GD.Print("Game path: " + MinorShift._Library.Sys.ExeDir);

        if (!EmueraThread.instance.Start(false))
            return false;

        working = true;
        firstWindow?.Hide();
        content?.Show();
        return true;
    }

    string currentGamePath = "";

    public void Clear() => pendingClear = true;
    public void Restart() => pendingRestart = true;

    public override void _Process(double delta)
    {
        if (pendingRestart)
        {
            // Clear와 Restart가 같은 프레임에 켜지면 DoClear가 두 번 실행되므로,
            // Restart를 우선해 한 번만 처리한다
            pendingRestart = false;
            pendingClear = false;
            DoRestart();
            return;
        }
        if (pendingClear)
        {
            pendingClear = false;
            DoClear();
            return;
        }

        if (!working)
            return;

        // uEmuera 설계에서는 MainWindow.Update() 가 화면 갱신의 구동원이지만,
        // 이식 시 호출자가 존재하지 않았다(= 화면이 전혀 갱신되지 않았다).
        // Godot 노드를 다루므로 메인 스레드인 여기서 매 프레임 호출한다.
        GlobalStatic.MainWindow?.Update();
        content?.SyncInputBar();
    }

    void DoClear()
    {
        // End 는 Join 으로 스레드 종료를 기다린다. 기다리지 않고 다음을 시작하면 엔진이 두 번 돈다.
        EmueraThread.instance.End();

        content?.Clear();

        var console = GlobalStatic.Console;
        console?.ClearDisplay();
        console?.Dispose();

        MinorShift.Emuera.Content.AppContents.UnloadContents();
        ConfigData.Instance.Clear();
        uEmuera.Utils.ResourceClear();
        SpriteManager.ForceClear();
        GC.Collect();

        working = false;
        content?.Hide();
        firstWindow?.Show();
        firstWindow?.Rescan();
    }

    void DoRestart()
    {
        var path = currentGamePath;
        DoClear();
        if (!string.IsNullOrEmpty(path))
            StartGame(path);
    }

    public bool IsWorking => working;

    public override void _Notification(int what)
    {
        // 앱 종료 시 엔진 스레드를 확실히 정리한다
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
            EmueraThread.instance.End(1000);
    }
}
