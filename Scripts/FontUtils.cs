using Godot;

/// <summary>
/// 폰트 로딩 유틸리티.
/// Fonts/ 폴더에 폰트 파일을 배치하면 자동으로 로드.
/// 우선순위: 한국어 전용 → 일본어 → CJK 통합 → 기본
/// </summary>
public static class FontUtils
{
    static string defaultFontName = "NotoSansKR";
    static FontFile? loadedFont;

    public static void SetDefaultFont(string name)
    {
        defaultFontName = name;
        loadedFont = null;
    }

    public static string DefaultFontName => defaultFontName;

    // 폰트 탐색 순서 (한국어 우선)
    static readonly string[] FontPaths =
    {
        // 한국어 전용 (권장)
        "res://Fonts/NotoSansKR-Regular.ttf",
        "res://Fonts/NotoSansKR-Regular.otf",
        "res://Fonts/NanumGothicCoding.ttf",
        "res://Fonts/NanumGothic.ttf",
        // 일본어 게임용
        "res://Fonts/NotoSansJP-Regular.ttf",
        "res://Fonts/msgothic.ttc",
        "res://Fonts/msgothic.ttf",
        // CJK 통합
        "res://Fonts/NotoSansCJKkr-Regular.otf",
        "res://Fonts/NotoSansCJK-Regular.ttc",
        // fallback
        "res://Fonts/NotoSansMono-Regular.ttf",
    };

    public static FontFile? GetFont()
    {
        if (loadedFont != null) return loadedFont;

        foreach (var p in FontPaths)
        {
            if (ResourceLoader.Exists(p))
            {
                loadedFont = GD.Load<FontFile>(p);
                GD.Print("[FontUtils] 로드: " + p);
                return loadedFont;
            }
        }

        GD.PushWarning("[FontUtils] 폰트 없음! Fonts/ 에 NotoSansKR-Regular.ttf 를 배치하세요.");
        return null;
    }
}
