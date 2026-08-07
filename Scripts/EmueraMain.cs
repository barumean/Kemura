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

        // 오디오 플레이어를 붙일 부모. Godot 노드는 메인 스레드에서만
        // 만들 수 있으므로 여기서 한 번 등록하고, 이후 요청은 큐를 지난다.
        EmAudio.Attach(this);

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

        // 켜져 있을 때만 덮어쓴다. 항상 대입하면 게임의 emuera.config 가
        // 이미 YES 로 지정한 경우를 우리가 NO 로 되돌려버린다.
        ConfigData.ForceCompatiErrorLine =
            Settings.ForceRunOnParseError ? true : (bool?)null;

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

            // ERB 파일이 하위 폴더에만 있는 게임이 흔하다.
            // (예: ERB/COMMON.ERB 없이 ERB/BODY_INFO/*.ERB 만 있는 구성)
            // 예전에는 최상위만 훑어서 그런 게임의 시작을 막아버렸다.
            // AllDirectories 로 훑고, 못 읽으면 그 사실을 구분해 알린다.
            var erbs = PathResolver.GetFiles(erbDir, "*.ERB",
                System.IO.SearchOption.AllDirectories);
            if (erbs.Length == 0)
            {
                // 권한이 없으면 Directory.Exists 는 true 여도 열거 결과가
                // 0건이 되는 경우가 있다(예외를 던지지 않음). 실제로 읽을 수
                // 있는지 따로 확인해서 원인을 구분한다.
                bool readable;
                try
                {
                    System.IO.Directory.GetFileSystemEntries(erbDir);
                    readable = true;
                }
                catch { readable = false; }

                if (!readable)
                    return PermissionMessage(erbDir);

                return $"ERB 폴더 안에 .ERB 파일이 없습니다: {erbDir}\n"
                     + "게임 압축을 풀 때 폴더 구조가 유지됐는지 확인해주세요.";
            }

            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return PermissionMessage(gameDir);
        }
        catch (Exception e)
        {
            return $"게임 폴더를 확인할 수 없습니다: {e.GetType().Name}: {e.Message}";
        }
    }

    static string PermissionMessage(string path)
    {
#if GODOT_ANDROID
        return $"'{path}' 를 읽을 권한이 없습니다.\n"
             + "설정 → 앱 → Kemura → 권한 → '모든 파일 접근'을 허용한 뒤 "
             + "앱으로 돌아오면 자동으로 다시 검색합니다.\n"
             + $"권한을 줄 수 없다면 게임을 {Settings.AppExternalGameRoot} 에 넣으면 "
             + "권한 없이 읽을 수 있습니다.";
#else
        return $"'{path}' 를 읽을 권한이 없습니다.";
#endif
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
        // 엔진 스레드가 넣은 오디오 요청을 메인 스레드에서 처리한다
        EmAudio.Pump();
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
        // EM 확장의 이름 기반 저장소도 게임 사이에 남으면 안 된다
        EmMapStore.ClearAll();
        EmDataTableStore.ClearAll();
        EmXmlStore.ClearAll();
        EmAudio.StopAll();
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
