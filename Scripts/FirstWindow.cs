using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// ゲーム選択画面。main.tscnの Main/FirstWindow に付く。
/// </summary>
public partial class FirstWindow : Control
{
    ItemList? gameList;
    Label? statusLabel;
    Button? startButton;
    Button? permButton;
    Button? browseButton;
    Button? rescanButton;
    Button? sharedButton;
    Button? appDirButton;
    Button? upButton;
    LineEdit? pathEdit;
    // 자체 폴더 브라우저. Godot 내장 FileDialog 는 쓰지 않는다.
    Control? browseLayer;
    ItemList? browseList;
    Label? browsePathLabel;
    /// <summary>브라우저가 현재 보고 있는 폴더.</summary>
    string browseDir = "";
    /// <summary>목록 각 행에 대응하는 실제 경로.</summary>
    readonly List<string> browseEntries = new();


    readonly List<string> gamePaths = new();

    string eraBaseDir = "";
    bool permissionRequested;

    public override void _Ready()
    {
        gameList = GetNodeOrNull<ItemList>("VBoxContainer/GameList");
        statusLabel = GetNodeOrNull<Label>("VBoxContainer/StatusLabel");
        startButton = GetNodeOrNull<Button>("VBoxContainer/HBox/StartButton");
        permButton = GetNodeOrNull<Button>("VBoxContainer/HBox/PermissionButton");

        pathEdit = GetNodeOrNull<LineEdit>("VBoxContainer/PathRow/PathEdit");
        browseButton = GetNodeOrNull<Button>("VBoxContainer/PathRow/BrowseButton");
        rescanButton = GetNodeOrNull<Button>("VBoxContainer/PathRow/RescanButton");
        browseLayer = GetNodeOrNull<Control>("BrowseLayer");
        browseList = GetNodeOrNull<ItemList>("BrowseLayer/Panel/VBox/List");
        browsePathLabel = GetNodeOrNull<Label>("BrowseLayer/Panel/VBox/PathLabel");
        // 한 번 탭으로 들어간다. ItemActivated(더블탭)는 모바일에서 어렵다.
        if (browseList != null)
            browseList.ItemSelected += OnBrowseItemSelected;
        WireBrowse("BrowseLayer/Panel/VBox/ButtonRow/UpDirButton", BrowseUp);
        WireBrowse("BrowseLayer/Panel/VBox/ButtonRow/PickButton", BrowsePick);
        WireBrowse("BrowseLayer/Panel/VBox/ButtonRow/CancelButton", CloseBrowser);

        // Godot의 FileDialog는 모바일에서 조작이 번거롭다.
        // 자주 쓰는 경로는 대화상자를 열지 않고 한 번에 지정한다.
        sharedButton = GetNodeOrNull<Button>("VBoxContainer/QuickRow/SharedButton");
        appDirButton = GetNodeOrNull<Button>("VBoxContainer/QuickRow/AppDirButton");
        upButton = GetNodeOrNull<Button>("VBoxContainer/QuickRow/UpButton");
        if (sharedButton != null)
            sharedButton.Pressed += () => SetGameRoot("/storage/emulated/0/emuera");
        if (appDirButton != null)
            appDirButton.Pressed += () => SetGameRoot(Settings.AppExternalGameRoot);
        if (upButton != null)
            upButton.Pressed += GoUp;

        if (startButton != null)
            startButton.Pressed += OnStartPressed;
        if (permButton != null)
            permButton.Pressed += OpenAllFilesAccessSettings;
        if (gameList != null)
            gameList.ItemActivated += _ => OnStartPressed();

        if (browseButton != null)
            browseButton.Pressed += OpenBrowser;
        if (rescanButton != null)
            rescanButton.Pressed += OnPathEntered;
        if (pathEdit != null)
            pathEdit.TextSubmitted += _ => OnPathEntered();

        // 첫 구동 시 권한을 먼저 요청한다.
        // 예전에는 Rescan 이 읽기에 실패한 뒤에야 요청했고, 그 결과 사용자에게는
        // "게임을 찾을 수 없습니다" 만 보였다. Android 6~10 은 런타임 팝업으로
        // 바로 받을 수 있으므로 화면을 보여주기 전에 요청하는 편이 낫다.
        RequestStoragePermissionOnce();

        eraBaseDir = Settings.EffectiveGameRoot;
        ApplyFontSize();
        Rescan();
    }

    /// <summary>
    /// 저장소 권한을 앱 실행 시 한 번 요청한다.
    ///
    /// Android 6~10 은 <c>OS.RequestPermissions()</c> 의 런타임 팝업으로 충분하다.
    /// Android 11+ 의 MANAGE_EXTERNAL_STORAGE 는 팝업으로 받을 수 없고 설정
    /// 앱에서 수동 허용해야 하며, Godot 에는 그 설정 화면을 여는 API 가 없다.
    /// 그래서 팝업을 띄운 뒤에도 읽을 수 없으면 Rescan 이 안내 문구와
    /// [권한 설정] 버튼을 보여준다.
    /// </summary>
    void RequestStoragePermissionOnce()
    {
#if GODOT_ANDROID
        if (permissionRequested)
            return;
        // 이미 읽을 수 있으면 팝업으로 사용자를 괴롭히지 않는다.
        if (HasStorageAccess())
            return;
        permissionRequested = true;
        OS.RequestPermissions();
#endif
    }

    /// <summary>
    /// 設定画面から戻ってきたときに再スキャンする。
    /// 以前は権限付与後に再スキャンする経路がなく、アプリ再起動が必要だった。
    /// </summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationResumed || what == NotificationWMWindowFocusIn)
        {
            if (Visible && permissionRequested)
            {
                // 사용자가 경로를 직접 지정하지 않았다면 기본 경로를 다시
                // 계산한다. DefaultGameRoot 는 "읽을 수 있는지"로 경로를
                // 고르므로, 권한을 받기 전에 계산한 값이 앱 전용 폴더로
                // 굳어 있을 수 있다.
                if (string.IsNullOrWhiteSpace(Settings.GameRoot))
                    eraBaseDir = Settings.EffectiveGameRoot;
                Rescan();
            }
        }
    }

    // ------------------------------------------------------------------
    // 文字サイズ
    // ------------------------------------------------------------------

    void ApplyFontSize()
    {
        int size = Settings.FontSize;
        var font = FontUtils.GetFont();

        // 一覧と本文プレビューは実際の表示サイズを反映させる
        foreach (var c in new Control?[] { gameList, statusLabel, pathEdit })
        {
            if (c == null) continue;
            c.AddThemeFontSizeOverride("font_size", size);
            if (font != null)
                c.AddThemeFontOverride("font", font);
        }
    }

    // ------------------------------------------------------------------
    // 経路
    // ------------------------------------------------------------------

    void WireBrowse(string path, Action handler)
    {
        var b = GetNodeOrNull<Button>(path);
        if (b == null)
        {
            GD.PushWarning($"FirstWindow: 버튼을 찾을 수 없습니다 ({path})");
            return;
        }
        b.Pressed += handler;
    }

    /// <summary>
    /// 자체 폴더 브라우저를 연다.
    ///
    /// Godot 의 FileDialog 는 모바일에서 행이 작고 더블탭이 필요하며
    /// 파일명 입력란·필터까지 딸려 나온다. 여기서는 폴더만 보여주고
    /// 한 번 탭으로 들어간다.
    /// </summary>
    void OpenBrowser()
    {
        // 현재 경로에서 시작한다. 없으면 위로 올라가며 열 수 있는 곳을 찾는다.
        var start = eraBaseDir;
        while (!string.IsNullOrEmpty(start) && !Directory.Exists(start))
        {
            var parent = Path.GetDirectoryName(start.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(parent) || parent == start) break;
            start = parent;
        }
        if (string.IsNullOrEmpty(start) || !Directory.Exists(start))
            start = FallbackBrowseRoot();

        SetLayerVisible(browseLayer, true);
        ShowBrowseDir(start);
    }

    void CloseBrowser() => SetLayerVisible(browseLayer, false);

    static void SetLayerVisible(Control? layer, bool visible)
    {
        if (layer == null) return;
        layer.Visible = visible;
        layer.MouseFilter = visible ? Control.MouseFilterEnum.Stop
                                    : Control.MouseFilterEnum.Ignore;
    }

    static string FallbackBrowseRoot()
    {
#if GODOT_ANDROID
        foreach (var c in new[] { "/storage/emulated/0", "/storage", "/" })
            if (Directory.Exists(c)) return c;
        return "/";
#else
        return Path.GetPathRoot(OS.GetUserDataDir()) ?? "/";
#endif
    }

    /// <summary>지정 폴더의 하위 폴더 목록을 보여준다.</summary>
    void ShowBrowseDir(string dir)
    {
        browseDir = Settings.NormalizeDir(dir).TrimEnd('/');
        if (browseDir.Length == 0) browseDir = "/";

        if (browsePathLabel != null)
            browsePathLabel.Text = browseDir;

        browseEntries.Clear();
        browseList?.Clear();

        string[] subs;
        try
        {
            subs = Directory.GetDirectories(browseDir);
        }
        catch (Exception e)
        {
            // 권한이 없는 폴더를 탭하면 여기로 온다. 목록만 비우고 알린다.
            browseList?.AddItem($"[읽을 수 없음: {e.GetType().Name}]");
            browseEntries.Add("");
            return;
        }

        Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
        foreach (var sub in subs)
        {
            var name = Path.GetFileName(sub);
            if (string.IsNullOrEmpty(name)) continue;
            // 게임 폴더인지 표시해주면 어디를 골라야 하는지 바로 보인다.
            bool isGame = IsValidGameDir(sub);
            browseList?.AddItem(isGame ? $"[게임] {name}" : $"{name}/");
            browseEntries.Add(sub);
        }
        if (browseEntries.Count == 0)
        {
            browseList?.AddItem("(하위 폴더가 없습니다)");
            browseEntries.Add("");
        }
    }

    void OnBrowseItemSelected(long index)
    {
        int i = (int)index;
        if (i < 0 || i >= browseEntries.Count) return;
        var target = browseEntries[i];
        if (string.IsNullOrEmpty(target)) return;   // 안내용 행
        ShowBrowseDir(target);
    }

    void BrowseUp()
    {
        var parent = Path.GetDirectoryName(browseDir.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(parent) || parent == browseDir)
        {
            SetStatus("더 위로 갈 수 없습니다.");
            return;
        }
        ShowBrowseDir(parent);
    }

    /// <summary>현재 보고 있는 폴더를 게임 루트로 확정한다.</summary>
    void BrowsePick()
    {
        CloseBrowser();
        SetGameRoot(browseDir);
    }

    void OnPathEntered()
    {
        SetGameRoot(pathEdit?.Text ?? "");
    }

    /// <summary>
    /// 한 단계 위로. 게임 폴더를 직접 지정해버린 경우 되돌아오기 쉽게 한다.
    /// (대화상자를 다시 열지 않아도 되게 하는 것이 목적)
    /// </summary>
    void GoUp()
    {
        var cur = eraBaseDir.TrimEnd('/', '\\');
        var parent = Path.GetDirectoryName(cur);
        if (string.IsNullOrEmpty(parent) || parent == cur)
        {
            SetStatus("더 위로 갈 수 없습니다.");
            return;
        }
        SetGameRoot(parent);
    }

    void SetGameRoot(string dir)
    {
        var norm = Settings.NormalizeDir(dir);
        if (string.IsNullOrEmpty(norm))
        {
            // 空にしたらプラットフォーム既定へ戻す
            Settings.GameRoot = "";
            eraBaseDir = Settings.EffectiveGameRoot;
            Rescan();
            return;
        }
        if (!Directory.Exists(norm))
        {
            // 앱 전용 폴더처럼 아직 없는 경로를 빠른 버튼으로 고를 수 있으므로
            // 만들어본다. 권한이 없어 못 만들면 그 사실을 그대로 알린다.
            try
            {
                Directory.CreateDirectory(norm);
                SetStatus($"폴더를 만들었습니다: {norm}");
            }
            catch (Exception e)
            {
                SetStatus($"폴더가 없고 만들 수도 없습니다: {norm}\n{e.Message}");
                return;
            }
        }
        Settings.GameRoot = norm;
        eraBaseDir = norm;
        Rescan();
    }

    // ------------------------------------------------------------------
    // 走査
    // ------------------------------------------------------------------

    /// <summary>ゲームフォルダを再走査する。</summary>
    public void Rescan()
    {
        // 앱을 켠 채로 게임을 복사해 넣는 경우가 흔하므로 캐시를 버리고 다시 훑는다
        PathResolver.ClearCache();
        gamePaths.Clear();
        gameList?.Clear();

        if (pathEdit != null && pathEdit.Text != eraBaseDir)
            pathEdit.Text = eraBaseDir;

        bool granted = HasStorageAccess();
        if (permButton != null)
            permButton.Visible = !granted;

        if (!granted)
        {
            SetStatus("파일 접근 권한이 필요합니다. [권한 설정]을 눌러 '모든 파일 접근'을 허용해주세요.");
            return;
        }

        // 権限があってもフォルダが無い場合は作成を試みる。
        // 権限拒否時はUnauthorizedAccessExceptionが飛ぶので必ず捕える
        // (_Ready内で例外が出るとシーン初期化が失敗する)。
        if (!Directory.Exists(eraBaseDir))
        {
            try
            {
                Directory.CreateDirectory(eraBaseDir);
            }
            catch (Exception e)
            {
                SetStatus($"게임 폴더를 만들 수 없습니다: {e.Message}");
                return;
            }
        }

        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories(eraBaseDir);
        }
        catch (Exception e)
        {
            SetStatus($"게임 폴더를 읽을 수 없습니다: {e.Message}");
            return;
        }

        Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs)
        {
            if (!IsValidGameDir(dir))
                continue;
            gamePaths.Add(dir);
            gameList?.AddItem(Path.GetFileName(dir));
        }

        if (gamePaths.Count == 0)
            SetStatus($"게임을 찾을 수 없습니다. 이 폴더 안에 ERB 폴더를 가진 게임 폴더를 넣거나, [찾아보기]로 다른 경로를 지정하세요.");
        else
            SetStatus($"{gamePaths.Count}개 게임 발견");
    }

    /// <summary>
    /// ERB 폴더(또는 emuera.config)를 가진 폴더만 게임으로 본다.
    ///
    /// Android/Linux는 대소문자를 구분하므로 Erb/ 처럼 섞인 표기의 게임은
    /// 목록에 아예 나타나지 않았다(Windows에서는 나타났다). PathResolver로
    /// 대소문자를 무시해 찾는다.
    /// </summary>
    static bool IsValidGameDir(string dir)
    {
        try
        {
            return Directory.Exists(PathResolver.ResolveDirectory(Path.Combine(dir, "erb"))) ||
                   File.Exists(PathResolver.ResolveFile(Path.Combine(dir, "emuera.config")));
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------
    // 権限
    // ------------------------------------------------------------------

    /// <summary>
    /// 実際に読めるかで判定する。
    /// 以前は OS.HasFeature("MANAGE_EXTERNAL_STORAGE") を見ていたが、
    /// Godotのfeature tagは "android"/"mobile" 等であって権限名ではないため
    /// 常にfalseを返し、しかも要求処理の中身が空だった。
    /// </summary>
    bool HasStorageAccess()
    {
#if GODOT_ANDROID
        // Android 6〜10はランタイム権限で足りる
        var granted = OS.GetGrantedPermissions();
        foreach (var p in granted)
        {
            if (p.EndsWith("READ_EXTERNAL_STORAGE") || p.EndsWith("MANAGE_EXTERNAL_STORAGE"))
                return true;
        }

        // MANAGE_EXTERNAL_STORAGEはランタイムダイアログでは取得できないので、
        // 実際にディレクトリを列挙できるかで確認する
        // eraBaseDir 에 의존하지 않는다. 이 판정은 eraBaseDir 이 정해지기 전
        // (_Ready 의 권한 요청 시점)에도 불리므로 고정 경로로 조사한다.
        try
        {
            if (!string.IsNullOrEmpty(eraBaseDir) && Directory.Exists(eraBaseDir))
            {
                Directory.GetDirectories(eraBaseDir);
                return true;
            }
            Directory.GetDirectories("/storage/emulated/0/");
            return true;
        }
        catch
        {
            // 판정 함수는 부수 효과를 갖지 않는다.
            // 예전에는 여기서 OS.RequestPermissions() 를 호출해, 실행 시
            // 명시적으로 요청하는 경로와 겹쳐 팝업이 두 번 떴다.
            return false;
        }
#else
        return true;
#endif
    }

    /// <summary>
    /// ストレージ権限を要求する。
    ///
    /// Android 6〜10は OS.RequestPermissions() のランタイムダイアログで足りる。
    /// Android 11+ の MANAGE_EXTERNAL_STORAGE はダイアログでは付与できず、
    /// 設定アプリでの手動許可が必須。Godotは該当Intentを直接投げるAPIを
    /// 持たないため、ここでは正直に手順を案内する。
    /// (アプリに戻ると _Notification が Rescan() を呼ぶ)
    /// </summary>
    void OpenAllFilesAccessSettings()
    {
        permissionRequested = true;
#if GODOT_ANDROID
        bool requested = OS.RequestPermissions();
        SetStatus(requested
            ? "권한을 확인했습니다. 여전히 목록이 비어 있으면 설정 → 앱 → Kemura → 권한 → '모든 파일 접근'을 허용해주세요."
            : "설정 → 앱 → Kemura → 권한 → '모든 파일 접근'을 허용한 뒤 앱으로 돌아오면 자동으로 다시 검색합니다.");
#else
        SetStatus("데스크톱에서는 별도 권한이 필요하지 않습니다.");
#endif
    }

    // ------------------------------------------------------------------
    // ゲーム開始
    // ------------------------------------------------------------------

    void OnStartPressed()
    {
        if (gameList == null) return;
        var selected = gameList.GetSelectedItems();
        if (selected.Length == 0)
        {
            SetStatus("게임을 선택해주세요.");
            return;
        }
        int idx = selected[0];
        if (idx < 0 || idx >= gamePaths.Count) return;

        // 絶対パス("/root/Main/EmueraMain")はmain.tscnがルートシーンでないと
        // 解決できず、以前はここが常にnullでボタンが無反応だった。
        var main = GetNodeOrNull<EmueraMain>("../EmueraMain");
        if (main == null)
        {
            SetStatus("내부 오류: EmueraMain 노드를 찾을 수 없습니다.");
            GD.PushError("FirstWindow: EmueraMain 을 찾을 수 없습니다 (../EmueraMain)");
            return;
        }

        string gamePath = gamePaths[idx] + "/";
        if (!main.StartGame(gamePath))
        {
            // 이유를 그대로 보여준다. 예전에는 "시작할 수 없습니다"만 남기고
            // 실제 원인은 로그로만 갔기 때문에 Android에서 진단이 불가능했다.
            var why = main.LastStartError;
            SetStatus(string.IsNullOrEmpty(why) ? "게임을 시작할 수 없습니다." : why);
        }
    }

    void SetStatus(string msg)
    {
        if (statusLabel != null)
            statusLabel.Text = msg;
        GD.Print("[FirstWindow] " + msg);
    }
}
