using Godot;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 게임 선택 화면. main.tscn 의 Main/FirstWindow 에 붙는다.
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
    /// 설정 화면에서 돌아왔을 때 다시 스캔한다.
    /// 이전에는 권한 부여 후 다시 스캔하는 경로가 없어 앱 재시작이 필요했다.
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
    // 글자 크기
    // ------------------------------------------------------------------

    void ApplyFontSize()
    {
        int size = Settings.FontSize;
        var font = FontUtils.GetFont();

        // 목록과 안내 문구도 게임 화면과 같은 크기로 맞춘다
        // (크기 조절 UI 자체는 게임 화면의 메뉴로 옮겼다)
        foreach (var c in new Control?[] { gameList, statusLabel, pathEdit })
        {
            if (c == null) continue;
            c.AddThemeFontSizeOverride("font_size", size);
            if (font != null)
                c.AddThemeFontOverride("font", font);
        }
    }

    // ------------------------------------------------------------------
    // 경로
    // ------------------------------------------------------------------

    void OpenDirDialog()
    {
        if (dirDialog == null)
        {
            SetStatus("내부 오류: 폴더 선택 대화상자를 찾을 수 없습니다.");
            return;
        }
        // 현재 경로에서 연다. 없으면 상위로 거슬러 열 수 있는 곳을 찾는다.
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
            // 비우면 플랫폼 기본값으로 되돌린다
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
    // 탐색
    // ------------------------------------------------------------------

    /// <summary>게임 폴더를 다시 탐색한다.</summary>
    public void Rescan()
    {
        gamePaths.Clear();
        gameList?.Clear();

        // 게임 화면 메뉴에서 글자 크기를 바꾼 뒤 목록으로 돌아온 경우를 위해
        // 다시 스캔할 때마다 크기를 재적용한다.
        ApplyFontSize();

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

        // 권한이 있어도 폴더가 없으면 생성을 시도한다.
        // 권한 거부 시 UnauthorizedAccessException 이 발생하므로 반드시 잡는다
        // (_Ready 안에서 예외가 나면 씬 초기화가 실패한다).
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

    static bool IsValidGameDir(string dir)
    {
        try
        {
            return Directory.Exists(Path.Combine(dir, "ERB")) ||
                   Directory.Exists(Path.Combine(dir, "erb")) ||
                   File.Exists(Path.Combine(dir, "emuera.config"));
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------------
    // 권한
    // ------------------------------------------------------------------

    /// <summary>
    /// 실제로 읽을 수 있는지로 판정한다.
    /// 이전에는 OS.HasFeature("MANAGE_EXTERNAL_STORAGE") 를 확인했지만,
    /// Godot의 feature tag는 "android"/"mobile" 등이고 권한 이름이 아니므로
    /// 항상 false를 반환했고, 게다가 요청 처리의 본문이 비어 있었다.
    /// </summary>
    bool HasStorageAccess()
    {
#if GODOT_ANDROID
        // Android 6~10 은 런타임 권한으로 충분하다
        var granted = OS.GetGrantedPermissions();
        foreach (var p in granted)
        {
            if (p.EndsWith("READ_EXTERNAL_STORAGE") || p.EndsWith("MANAGE_EXTERNAL_STORAGE"))
                return true;
        }

        // MANAGE_EXTERNAL_STORAGE 는 런타임 대화상자로 얻을 수 없으므로,
        // 실제로 디렉터리를 열거할 수 있는지로 확인한다
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
    /// 저장소 권한을 요청한다.
    ///
    /// Android 6~10 은 OS.RequestPermissions() 의 런타임 대화상자로 충분하다.
    /// Android 11+ 의 MANAGE_EXTERNAL_STORAGE 는 대화상자로 부여할 수 없고,
    /// 설정 앱에서 수동 허용이 필수다. Godot은 해당 Intent를 직접 던지는 API가
    /// 없으므로 여기서는 정직하게 절차를 안내한다.
    /// (앱으로 돌아오면 _Notification 이 Rescan() 을 호출한다)
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
    // 게임 시작
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

        // 절대 경로("/root/Main/EmueraMain")는 main.tscn 이 루트 씬이 아니면
        // 해석되지 않아, 이전에는 여기가 항상 null 이고 버튼이 무반응이었다.
        var main = GetNodeOrNull<EmueraMain>("../EmueraMain");
        if (main == null)
        {
            SetStatus("내부 오류: EmueraMain 노드를 찾을 수 없습니다.");
            GD.PushError("FirstWindow: EmueraMain 을 찾을 수 없습니다 (../EmueraMain)");
            return;
        }

        string gamePath = gamePaths[idx] + "/";
        if (!main.StartGame(gamePath))
            SetStatus("게임을 시작할 수 없습니다.");
    }

    void SetStatus(string msg)
    {
        if (statusLabel != null)
            statusLabel.Text = msg;
        GD.Print("[FirstWindow] " + msg);
    }
}
