using Godot;
using System;
using MinorShift.Emuera;

/// <summary>
/// ゲーム本体の制御。main.tscnの Main/EmueraMain に付く。
///
/// 起動時にゲームを開始してはいけない(まずFirstWindowでゲームを選ばせる)。
/// ゲーム開始はFirstWindow.StartGame()から呼ばれる。
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
        // Loggerは何よりも先に差しておく(以降のログを落とさないため)
        uEmuera.Logger.info = GD.Print;
        uEmuera.Logger.warn = s => GD.PushWarning(s?.ToString() ?? "");
        uEmuera.Logger.error = s => GD.PushError(s?.ToString() ?? "");

        // 絶対パス("/root/Main/...")を埋め込むと、このシーンを子として
        // インスタンス化した瞬間に壊れる。兄弟ノードは相対パスで引く。
        content = GetNodeOrNull<EmueraContent>("../EmueraContent");
        firstWindow = GetNodeOrNull<FirstWindow>("../FirstWindow");

        if (content == null)
            GD.PushError("EmueraMain: EmueraContent が見つかりません (../EmueraContent)");
        if (firstWindow == null)
            GD.PushError("EmueraMain: FirstWindow が見つかりません (../FirstWindow)");

        GenericUtils.SetContent(content);

        // 起動直後はゲーム選択画面のみを見せる
        content?.Hide();
        firstWindow?.Show();
    }

    /// <summary>FirstWindowから呼ばれる。gamePathはゲームフォルダの絶対パス。</summary>
    internal bool StartGame(string gamePath)
    {
        if (working)
        {
            GD.PushWarning("EmueraMain.StartGame: already working");
            return false;
        }
        if (string.IsNullOrEmpty(gamePath))
            return false;

        // Sys.ExeDirはprivate setなので直接代入できない(CS0272)。
        // 正しい入口は SetWorkFolder(親) + SetSourceFolder(フォルダ名) で、
        // これがExeDirを正規化して組み立てる。
        var full = gamePath.TrimEnd('/', '\\');
        var parent = System.IO.Path.GetDirectoryName(full);
        var name = System.IO.Path.GetFileName(full);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            GD.PushError($"EmueraMain.StartGame: 不正なゲームパス '{gamePath}'");
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
            // ClearとRestartが同一フレームで立つとDoClearが二重に走るため、
            // Restartを優先して1回だけ処理する
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

        // uEmueraの設計ではMainWindow.Update()が表示更新の駆動源だが、
        // 移植時に呼び出し元が存在しなかった(=画面が一切更新されなかった)。
        // Godotノードを触るのでメインスレッドであるここから毎フレーム呼ぶ。
        GlobalStatic.MainWindow?.Update();
        content?.SyncInputBar();
    }

    void DoClear()
    {
        // Endはスレッドの終了をJoinで待つ。待たずに次を開始するとエンジンが二重に走る。
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
        // アプリ終了時にエンジンスレッドを確実に畳む
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
            EmueraThread.instance.End(1000);
    }
}
