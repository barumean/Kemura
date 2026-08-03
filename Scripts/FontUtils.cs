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
                "日本語フォントが見つかりません (Fonts/ が空です)。" +
                "Godot標準フォントはCJKを含まないため日本語が表示されません。" +
                "Fonts/NotoSansJP-Regular.ttf を配置してください。詳細は Fonts/README.md 参照。");
        }
        return null;
    }
}
