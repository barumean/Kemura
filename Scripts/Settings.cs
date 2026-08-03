using Godot;
using System;

/// <summary>
/// 사용자 설정 영속화. user://settings.cfg 에 저장된다.
/// (Android에서는 앱 전용 저장소이므로 별도 권한이 필요 없다.)
/// </summary>
internal static class Settings
{
    const string Path = "user://settings.cfg";
    const string Sec = "kemura";

    public const int MinFontSize = 12;
    public const int MaxFontSize = 64;
    public const int DefaultFontSize = 28;   // 모바일 기본값. 20은 폰에서 너무 작다.

    static int fontSize = DefaultFontSize;
    static string gameRoot = "";
    static bool loaded;

    /// <summary>콘솔 텍스트 표시 크기(px).</summary>
    public static int FontSize
    {
        get { Load(); return fontSize; }
        set
        {
            Load();
            var v = Mathf.Clamp(value, MinFontSize, MaxFontSize);
            if (v == fontSize) return;
            fontSize = v;
            Save();
        }
    }

    /// <summary>게임 폴더들이 들어있는 루트 경로. 빈 문자열이면 플랫폼 기본값을 사용.</summary>
    public static string GameRoot
    {
        get { Load(); return gameRoot; }
        set
        {
            Load();
            var v = value ?? "";
            if (v == gameRoot) return;
            gameRoot = v;
            Save();
        }
    }

    /// <summary>설정이 비어 있을 때 사용할 플랫폼별 기본 경로.</summary>
    public static string DefaultGameRoot
    {
        get
        {
#if GODOT_ANDROID
            return "/storage/emulated/0/emuera/";
#else
            var beside = OS.GetExecutablePath().GetBaseDir().PathJoin("emuera") + "/";
            if (System.IO.Directory.Exists(beside))
                return beside;
            return OS.GetUserDataDir() + "/emuera/";
#endif
        }
    }

    /// <summary>실제로 사용할 경로(설정값이 있으면 그것, 없으면 기본값).</summary>
    public static string EffectiveGameRoot
    {
        get
        {
            var r = GameRoot;
            if (string.IsNullOrWhiteSpace(r))
                r = DefaultGameRoot;
            return NormalizeDir(r);
        }
    }

    public static string NormalizeDir(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return "";
        p = p.Replace('\\', '/').TrimEnd('/');
        return p + "/";
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;

        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok)
            return;   // 파일이 없으면 기본값 유지

        fontSize = Mathf.Clamp(
            (int)cfg.GetValue(Sec, "font_size", DefaultFontSize), MinFontSize, MaxFontSize);
        gameRoot = (string)cfg.GetValue(Sec, "game_root", "");
    }

    static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(Sec, "font_size", fontSize);
        cfg.SetValue(Sec, "game_root", gameRoot);
        var err = cfg.Save(Path);
        if (err != Error.Ok)
            GD.PushWarning($"설정을 저장할 수 없습니다 ({err})");
    }
}
