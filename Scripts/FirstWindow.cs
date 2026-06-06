using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class FirstWindow : Control
{
    ItemList? gameList;
    Label? statusLabel;
    Button? startButton;
    Label? pathLabel;

    readonly List<string> gamePaths = new();

    string eraBaseDir = "";

    public override void _Ready()
    {
        gameList = GetNodeOrNull<ItemList>("VBoxContainer/GameList");
        statusLabel = GetNodeOrNull<Label>("VBoxContainer/StatusLabel");
        startButton = GetNodeOrNull<Button>("VBoxContainer/HBox/StartButton");
        pathLabel = GetNodeOrNull<Label>("VBoxContainer/PathLabel");

        if (startButton != null)
            startButton.Pressed += OnStartPressed;

        RequestPermissionsAndScan();
    }

    void RequestPermissionsAndScan()
    {
#if GODOT_ANDROID
        if (!OS.HasFeature("MANAGE_EXTERNAL_STORAGE"))
        {
            // Request permissions via Android
        }
        eraBaseDir = "/storage/emulated/0/emuera/";
        if (!Directory.Exists(eraBaseDir))
        {
            Directory.CreateDirectory(eraBaseDir);
        }
#else
        eraBaseDir = OS.GetExecutablePath().GetBaseDir().PathJoin("emuera") + "/";
        if (!Directory.Exists(eraBaseDir))
            eraBaseDir = OS.GetUserDataDir() + "/emuera/";
#endif
        ScanGames();
    }

    void ScanGames()
    {
        gamePaths.Clear();
        if (gameList != null) gameList.Clear();

        if (!Directory.Exists(eraBaseDir))
        {
            SetStatus($"게임 폴더가 없습니다: {eraBaseDir}");
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
            SetStatus("게임을 찾을 수 없습니다. emuera 폴더에 게임을 넣어주세요.");
        else
            SetStatus($"{gamePaths.Count}개 게임 발견");
    }

    bool IsValidGameDir(string dir)
    {
        return Directory.Exists(Path.Combine(dir, "ERB")) ||
               Directory.Exists(Path.Combine(dir, "erb")) ||
               File.Exists(Path.Combine(dir, "emuera.config"));
    }

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

        string gamePath = gamePaths[idx] + "/";
        MinorShift._Library.Sys.ExeDir = gamePath;

        var main = GetNodeOrNull<EmueraMain>("/root/Main/EmueraMain");
        if (main != null)
        {
            Hide();
            var content = GetNodeOrNull<EmueraContent>("/root/Main/EmueraContent");
            GenericUtils.SetContent(content!);
            EmueraThread.instance.Start(false, false);
        }
    }

    void SetStatus(string msg)
    {
        if (statusLabel != null)
            statusLabel.Text = msg;
        GD.Print("[FirstWindow] " + msg);
    }
}
