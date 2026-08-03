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
    /// 日本語フォントを返す。見つからない場合はnull。
    ///
    /// Godot標準フォントにはCJKグリフが含まれないため、フォントが無いと
    /// era系ゲームの日本語テキストが豆腐(□)になる。以前は黙ってnullを
    /// 返していたので原因が分からなかった。一度だけ警告を出す。
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
