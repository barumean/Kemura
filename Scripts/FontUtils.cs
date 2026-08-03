using Godot;
using System;
using System.Collections.Generic;

public static class FontUtils
{
    static string defaultFontName = "MS Gothic";
    static FontFile? loadedFont;
    static bool warned;

    public static void SetDefaultFont(string name)
    {
        defaultFontName = name;
    }

    public static string DefaultFontName => defaultFontName;

    /// <summary>
    /// 일본어 폰트를 반환한다. 찾지 못하면 null.
    ///
    /// Godot 기본 폰트에는 CJK 글리프가 없어서, 폰트가 없으면
    /// era 계열 게임의 일본어 텍스트가 두부(□)가 된다. 이전에는 조용히 null을
    /// 반환해서 원인을 알 수 없었다. 한 번만 경고를 출력한다.
    /// </summary>
    public static FontFile? GetFont()
    {
        if (loadedFont != null) return loadedFont;

        var paths = new[]
        {
            "res://Fonts/msgothic.ttc",
            "res://Fonts/msgothic.ttf",
            "res://Fonts/NotoSansJP-Regular.ttf",
            "res://Fonts/NotoSansMono.ttf",
        };
        foreach (var p in paths)
        {
            if (ResourceLoader.Exists(p))
            {
                loadedFont = GD.Load<FontFile>(p);
                if (loadedFont != null)
                    return loadedFont;
            }
        }

        if (!warned)
        {
            warned = true;
            GD.PushWarning(
                "일본어 폰트를 찾을 수 없습니다 (Fonts/ 가 비어 있습니다). " +
                "Godot 기본 폰트는 CJK를 포함하지 않아 일본어가 표시되지 않습니다. " +
                "Fonts/NotoSansJP-Regular.ttf 를 배치해주세요. 자세한 내용은 Fonts/README.md 참조.");
        }
        return null;
    }
}
