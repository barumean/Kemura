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
    LineEdit? pathEdit;
    FileDialog? dirDialog;


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
        dirDialog = GetNodeOrNull<FileDialog>("DirDialog");

        if (startButton != null)
            startButton.Pressed += OnStartPressed;
        if (permButton != null)
            permButton.Pressed += OpenAllFilesAccessSettings;
        if (gameList != null)
            gameList.ItemActivated += _ => OnStartPressed();

        if (browseButton != null)
            browseButton.Pressed += OpenDirDialog;
        if (rescanButton != null)
            rescanButton.Pressed += OnPathEntered;
        if (pathEdit != null)
            pathEdit.TextSubmitted += _ => OnPathEntered();
        if (dirDialog != null)
            dirDialog.DirSelected += OnDirSelected;

        eraBaseDir = Settings.EffectiveGameRoot;
        ApplyFontSize();
        Rescan();
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
                Rescan();
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

    void OpenDirDialog()
    {
        if (dirDialog == null)
        {
            SetStatus("내부 오류: 폴더 선택 대화상자를 찾을 수 없습니다.");
            return;
        }
        // 現在の経路から開く。存在しない場合は上位に遡って開けるところを探す。
        var start = eraBaseDir;
        while (!string.IsNullOrEmpty(start) && !Directory.Exists(start))
        {
            var parent = Path.GetDirectoryName(start.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(parent) || parent == start) break;
            start = parent;
        }
#if GODOT_ANDROID
        if (string.IsNullOrEmpty(start) || !Directory.Exists(start))
            start = "/storage/emulated/0";
#endif
        if (Directory.Exists(start))
            dirDialog.CurrentDir = start;

        dirDialog.PopupCentered();
    }

    void OnDirSelected(string dir)
    {
        SetGameRoot(dir);
    }

    void OnPathEntered()
    {
        SetGameRoot(pathEdit?.Text ?? "");
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
            SetStatus($"폴더가 없습니다: {norm}");
            return;
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
        try
        {
            if (Directory.Exists(eraBaseDir))
            {
                Directory.GetDirectories(eraBaseDir);
                return true;
            }
            Directory.GetDirectories("/storage/emulated/0/");
            return true;
        }
        catch
        {
            if (!permissionRequested)
            {
                permissionRequested = true;
                OS.RequestPermissions();
            }
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
