using Godot;
using System;
using System.Collections.Concurrent;
using System.Threading;
using MinorShift.Emuera;

public partial class EmueraMain : Node
{
    bool working;
    bool pendingClear;
    bool pendingRestart;

    public override void _Ready()
    {
        uEmuera.Logger.info = GD.Print;
        uEmuera.Logger.warn = s => GD.PushWarning(s?.ToString() ?? "");
        uEmuera.Logger.error = s => GD.PushError(s?.ToString() ?? "");

        CallDeferred(MethodName.StartGame);
    }

    void StartGame()
    {
        var content = GetNode<EmueraContent>("/root/Main/EmueraContent");
        GenericUtils.SetContent(content);

        SetGamePath();
        EmueraThread.instance.Start(false, false);
        working = true;

        GetNode<FirstWindow>("/root/FirstWindow")?.Hide();
    }

    void SetGamePath()
    {
        string basePath;
#if GODOT_ANDROID
        basePath = "/storage/emulated/0/emuera/";
        if (!System.IO.Directory.Exists(basePath))
            basePath = OS.GetUserDataDir() + "/emuera/";
#else
        basePath = OS.GetExecutablePath().GetBaseDir().PathJoin("emuera") + "/";
        if (!System.IO.Directory.Exists(basePath))
            basePath = OS.GetUserDataDir() + "/emuera/";
#endif
        MinorShift._Library.Sys.ExeDir = basePath;
        GD.Print("Game path: " + basePath);
    }

    public void Clear()
    {
        pendingClear = true;
    }

    public void Restart()
    {
        pendingRestart = true;
    }

    public override void _Process(double delta)
    {
        if (pendingClear)
        {
            pendingClear = false;
            DoClear();
        }
        if (pendingRestart)
        {
            pendingRestart = false;
            DoRestart();
        }
    }

    void DoClear()
    {
        EmueraThread.instance.End();
        var content = GetNode<EmueraContent>("/root/Main/EmueraContent");
        content?.Clear();
        var console = GlobalStatic.Console;
        console?.ClearDisplay();
        console?.Dispose();
        MinorShift.Emuera.Content.AppContents.UnloadContents();
        ConfigData.Instance.Clear();
        SpriteManager.ForceClear();
        GC.Collect();
        working = false;
        GetNode<FirstWindow>("/root/FirstWindow")?.Show();
    }

    void DoRestart()
    {
        DoClear();
        StartGame();
    }

    public bool IsWorking => working;
}
