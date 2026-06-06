using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class FirstWindow : Control
{
    ItemList? gameList;
    Label? statusLabel;
    Button? startButton;
    Button? refreshButton;
    Label? pathLabel;

    readonly List<string> gamePaths = new();
    string eraBaseDir = "";

    public override void _Ready()
    {
        gameList    = GetNodeOrNull<ItemList>("VBoxContainer/GameList");
        statusLabel = GetNodeOrNull<Label>("VBoxContainer/StatusLabel");
        startButton = GetNodeOrNull<Button>("VBoxContainer/HBox/StartButton");
        refreshButton = GetNodeOrNull<Button>("VBoxContainer/HBox/RefreshButton");
        pathLabel   = GetNodeOrNull<Label>("VBoxContainer/PathLabel");

        if (startButton != null)
            startButton.Pressed += OnStartPressed;
        if (refreshButton != null)
            refreshButton.Pressed += () => ScanGames();

        RequestPermissionsAndScan();
    }

    // ── 권한 요청 + 기본 경로 설정 ─────────────────────────────

    void RequestPermissionsAndScan()
    {
#if GODOT_ANDROID
        OS.RequestPermissions();
        eraBaseDir = "/storage/emulated/0/emuera/";
        if (!Directory.Exists(eraBaseDir))
        {
            try { Directory.CreateDirectory(eraBaseDir); }
            catch { eraBaseDir = OS.GetUserDataDir() + "/emuera/"; }
        }
#else
        eraBaseDir = OS.GetExecutablePath().GetBaseDir().PathJoin("emuera") + "/";
        if (!Directory.Exists(eraBaseDir))
            eraBaseDir = OS.GetUserDataDir() + "/emuera/";
#endif
        ScanGames();
    }

    // ── 게임 폴더 스캔 ──────────────────────────────────────────
    // eramaerb: 게임 폴더는 ERB/ 또는 erb/ 하위 폴더와
    //           emuera.config 파일로 식별 (eramaerc 스펙)

    void ScanGames()
    {
        gamePaths.Clear();
        gameList?.Clear();

        if (!Directory.Exists(eraBaseDir))
        {
            SetStatus($"폴더 없음: {eraBaseDir}");
            return;
        }

        if (pathLabel != null)
            pathLabel.Text = $"경로: {eraBaseDir}";

        foreach (var dir in Directory.GetDirectories(eraBaseDir))
        {
            if (IsValidGameDir(dir))
            {
                gamePaths.Add(dir);
                gameList?.AddItem(Path.GetFileName(dir));
            }
        }

        if (gamePaths.Count == 0)
            SetStatus($"게임 없음 — {eraBaseDir} 에 게임 폴더를 넣어주세요.");
        else
            SetStatus($"{gamePaths.Count}개 게임 발견");
    }

    /// eramaerc 스펙: 게임 폴더는 ERB/, CSV/ 또는 emuera.config를 포함해야 함
    bool IsValidGameDir(string dir)
    {
        // eramaerb: .ERB 파일을 포함하는 ERB 폴더 존재
        if (Directory.Exists(Path.Combine(dir, "ERB")) ||
            Directory.Exists(Path.Combine(dir, "erb")))
            return true;
        // eramaerc: emuera.config 파일 존재
        if (File.Exists(Path.Combine(dir, "emuera.config")))
            return true;
        return false;
    }

    // ── 게임 시작 ────────────────────────────────────────────────

    void OnStartPressed()
    {
        if (gameList == null) return;

        int[] selected = gameList.GetSelectedItems();
        if (selected.Length == 0)
        {
            SetStatus("게임을 선택하세요.");
            return;
        }

        int idx = selected[0];
        if (idx < 0 || idx >= gamePaths.Count) return;

        // 경로 끝에 / 보장 (eramaerc: ExeDir는 항상 / 종료)
        string gamePath = gamePaths[idx].TrimEnd('/', '\\') + "/";

        // GameState에 저장 후 main.tscn으로 장면 전환
        // (main.tscn의 EmueraMain._Ready()에서 이 경로를 읽어 엔진 시작)
        GameState.SelectedGamePath = gamePath;
        GD.Print("[FirstWindow] Selected game: " + gamePath);

        GetTree().ChangeSceneToFile("res://main.tscn");
    }

    void SetStatus(string msg)
    {
        if (statusLabel != null)
            statusLabel.Text = msg;
        GD.Print("[FirstWindow] " + msg);
    }
}
