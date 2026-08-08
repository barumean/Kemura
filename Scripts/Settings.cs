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
    static bool forceRunOnParseError;
    static bool showNumPad = true;
    static bool loaded;

    /// <summary>
    /// 해석할 수 없는 ERB 행이 있어도 게임을 강제로 실행한다.
    /// (emuera.config 의 「解釈不可能な行があっても実行する」 에 대응)
    ///
    /// EmueraEE 확장 명령(DT_*, MAP_*, XML_*, PLAYBGM 등)을 쓰는 게임은
    /// 이 엔진에서 해당 행을 해석하지 못해 시작 자체가 막힌다. 이 옵션을 켜면
    /// 일단 실행되지만, 그 행이 실제로 실행되는 지점에서는 오작동한다.
    /// </summary>
    public static bool ForceRunOnParseError
    {
        get { Load(); return forceRunOnParseError; }
        set
        {
            Load();
            if (value == forceRunOnParseError) return;
            forceRunOnParseError = value;
            Save();
        }
    }

    /// <summary>
    /// 숫자 키패드를 띄워둘지. 기본 켬.
    ///
    /// 항상 보이게 해달라는 요청이라 기본값을 켬으로 두지만, 세로가 짧은
    /// 기기에서는 화면을 많이 차지한다. 사용자가 접으면 그 선택을 기억한다.
    /// </summary>
    public static bool ShowNumPad
    {
        get { Load(); return showNumPad; }
        set
        {
            Load();
            if (value == showNumPad) return;
            showNumPad = value;
            Save();
        }
    }

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

    /// <summary>
    /// 앱 전용 외부 저장소. Android 에서 <b>아무 권한 없이</b> 읽고 쓸 수 있는
    /// 유일한 외부 경로다(getExternalFilesDir 과 같은 위치).
    ///
    /// Android 11+ 의 MANAGE_EXTERNAL_STORAGE 는 런타임 팝업으로 받을 수 없고
    /// 설정 앱에서 수동 허용해야 한다. 그걸 원하지 않거나 기기 정책상 막혀
    /// 있는 경우의 탈출구로 쓴다. PC 에서 USB(MTP)로 이 경로에 게임을 넣으면
    /// 권한 없이 바로 동작한다.
    /// </summary>
    public static string AppExternalGameRoot =>
        $"/storage/emulated/0/Android/data/{PackageName}/files/emuera/";

    /// <summary>
    /// 앱 패키지명(applicationId).
    /// <c>export_presets.cfg</c> 의 <c>package/unique_name</c> 과 반드시 같아야 한다.
    ///
    /// 예전에는 이 문자열이 Settings 와 FirstWindow 두 곳에 각각 하드코딩돼
    /// 있었다. 한쪽만 고치면 앱 전용 폴더 경로와 권한 설정 Intent 가 서로
    /// 다른 패키지를 가리켜 조용히 어긋난다.
    ///
    /// 바꾸면 기존 설치본의 앱 전용 폴더(게임 데이터)에 접근할 수 없게 되므로
    /// 출시 후에는 바꾸지 않는다.
    /// </summary>
    public const string PackageName = "com.kemura.emuera";

    /// <summary>설정이 비어 있을 때 사용할 플랫폼별 기본 경로.</summary>
    public static string DefaultGameRoot
    {
        get
        {
#if GODOT_ANDROID
            // 권한이 있으면 눈에 잘 보이는 곳을 우선한다.
            const string shared = "/storage/emulated/0/emuera/";
            if (CanList(shared))
                return shared;
            // 권한이 없어도 읽히는 앱 전용 경로에 게임이 있으면 그쪽을 쓴다.
            if (CanList(AppExternalGameRoot))
                return AppExternalGameRoot;
            return shared;
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

    /// <summary>실제로 목록을 읽을 수 있는지. 권한이 없으면 false.</summary>
    static bool CanList(string dir)
    {
        try
        {
            return System.IO.Directory.Exists(dir)
                && System.IO.Directory.GetFileSystemEntries(dir) != null;
        }
        catch
        {
            return false;
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
        forceRunOnParseError = (bool)cfg.GetValue(Sec, "force_run_on_parse_error", false);
        showNumPad = (bool)cfg.GetValue(Sec, "show_num_pad", true);
    }

    static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(Sec, "font_size", fontSize);
        cfg.SetValue(Sec, "game_root", gameRoot);
        cfg.SetValue(Sec, "force_run_on_parse_error", forceRunOnParseError);
        cfg.SetValue(Sec, "show_num_pad", showNumPad);
        var err = cfg.Save(Path);
        if (err != Error.Ok)
            GD.PushWarning($"설정을 저장할 수 없습니다 ({err})");
    }
}
