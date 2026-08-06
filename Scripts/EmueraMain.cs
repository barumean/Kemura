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
        // GD.Print は Print(params object[]) なので Action<object> に
        // メソッドグループのまま代入できない (CS0123)。ラムダで包む。
        uEmuera.Logger.info = s => GD.Print(s?.ToString() ?? "");
        uEmuera.Logger.warn = s => GD.PushWarning(s?.ToString() ?? "");
        uEmuera.Logger.error = s => GD.PushError(s?.ToString() ?? "");

        // SHIFT-JIS製 emuera.config のキー名を解決するための変換テーブル。
        // ゲーム読み込みより前に入れておく必要がある。
        ConfigMaps.Load();

        // --kemura-selftest: 대소문자 파일 해석을 실제 파일로 검증하고 종료한다.
        // CI가 Linux(대소문자 구분)에서 도는데도 이 버그를 놓쳤기 때문에 넣었다.
        if (SelfTest.Requested())
        {
            int code = SelfTest.Run();
            GetTree().Quit(code);
            return;
        }

        // 絶対パス("/root/Main/...")を埋め込むと、このシーンを子として
        // インスタンス化した瞬間に壊れる。兄弟ノードは相対パスで引く。
        content = GetNodeOrNull<EmueraContent>("../EmueraContent");
        firstWindow = GetNodeOrNull<FirstWindow>("../FirstWindow");

        if (content == null)
            GD.PushError("EmueraMain: EmueraContent 를 찾을 수 없습니다 (../EmueraContent)");
        if (firstWindow == null)
            GD.PushError("EmueraMain: FirstWindow 를 찾을 수 없습니다 (../FirstWindow)");

        GenericUtils.SetContent(content);

        // 起動直後はゲーム選択画面のみを見せる
        content?.Hide();
        firstWindow?.Show();
    }

    /// <summary>FirstWindowから呼ばれる。gamePathはゲームフォルダの絶対パス。</summary>
    internal bool StartGame(string gamePath)
    {
        lastStartError = "";
        if (working)
        {
            lastStartError = "이미 게임이 실행 중입니다.";
            GD.PushWarning("EmueraMain.StartGame: already working");
            return false;
        }
        if (string.IsNullOrEmpty(gamePath))
        {
            lastStartError = "게임 경로가 비어 있습니다.";
            return false;
        }

        // Sys.ExeDirはprivate setなので直接代入できない(CS0272)。
        // 正しい入口は SetWorkFolder(親) + SetSourceFolder(フォルダ名) で、
        // これがExeDirを正規化して組み立てる。
        var full = gamePath.TrimEnd('/', '\\');
        var parent = System.IO.Path.GetDirectoryName(full);
        var name = System.IO.Path.GetFileName(full);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            lastStartError = $"잘못된 게임 경로입니다: {gamePath}";
            GD.PushError("EmueraMain.StartGame: " + lastStartError);
            return false;
        }
        MinorShift._Library.Sys.SetWorkFolder(parent);
        MinorShift._Library.Sys.SetSourceFolder(name);
        currentGamePath = full;
        GD.Print("Game path: " + MinorShift._Library.Sys.ExeDir);

        // 이전 게임의 대소문자 캐시가 남아 있으면 안 된다
        PathResolver.ClearCache();

        var problem = DescribeGameFolderProblem(full);
        if (problem != null)
        {
            GD.PushError("EmueraMain.StartGame: " + problem);
            lastStartError = problem;
            return false;
        }

        if (!EmueraThread.instance.Start(false))
        {
            lastStartError = "엔진 스레드를 시작할 수 없습니다.";
            return false;
        }

        working = true;
        firstWindow?.Hide();
        content?.Show();
        return true;
    }

    string currentGamePath = "";

    /// <summary>StartGame이 false를 돌려준 이유. FirstWindow가 화면에 띄운다.</summary>
    internal string LastStartError => lastStartError;
    string lastStartError = "";

    /// <summary>
    /// 게임을 시작하기 전에 필수 구성을 확인한다. 문제가 없으면 null.
    ///
    /// Android에는 보이는 콘솔이 없어서, 지금까지 폴더 구성이 잘못됐을 때
    /// 사용자에게는 아무 설명 없이 빈 화면만 보였다. PC에서는 stdout으로
    /// 원인이 보였기 때문에 "PC에서는 되는데 폰에서는 안 된다"가 됐다.
    /// </summary>
    static string? DescribeGameFolderProblem(string gameDir)
    {
        try
        {
            if (!System.IO.Directory.Exists(gameDir))
                return $"게임 폴더가 없습니다: {gameDir}";

            // 엔진은 erb/ 와 ERB/ 만 찾는다. Erb/ 처럼 섞인 표기는
            // Windows에서만 열렸으므로 여기서도 대소문자를 무시해 찾는다.
            var erbDir = PathResolver.ResolveDirectory(
                System.IO.Path.Combine(gameDir, "erb"));
            if (!System.IO.Directory.Exists(erbDir))
                return "ERB 폴더를 찾을 수 없습니다. 게임 폴더 안에 ERB 폴더가 그대로 들어 있어야 합니다 "
                     + $"(선택한 폴더: {gameDir})";

            bool hasErb = false;
            foreach (var f in System.IO.Directory.EnumerateFiles(erbDir))
            {
                if (f.EndsWith(".ERB", StringComparison.OrdinalIgnoreCase))
                {
                    hasErb = true;
                    break;
                }
            }
            if (!hasErb)
                return $"ERB 폴더에 .ERB 파일이 없습니다: {erbDir}";

            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return "게임 폴더를 읽을 권한이 없습니다. 설정 → 앱 → Kemura → 권한에서 "
                 + "'모든 파일 접근'을 허용해주세요.";
        }
        catch (Exception e)
        {
            return $"게임 폴더를 확인할 수 없습니다: {e.GetType().Name}: {e.Message}";
        }
    }

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
        // 다른 게임으로 바꿀 때 이전 게임 폴더의 목록을 계속 보지 않도록 비운다
        PathResolver.ClearCache();
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
