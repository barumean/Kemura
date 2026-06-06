using Godot;
using System;
using System.Collections.Generic;

public static class FontUtils
{
    static string defaultFontName = "MS Gothic";
    static FontFile? loadedFont;

    public static void SetDefaultFont(string name)
    {
        defaultFontName = name;
    }

    public static string DefaultFontName => defaultFontName;

    public static FontFile? GetFont()
    {
        if (loadedFont != null) return loadedFont;

        var paths = new[]
        {
            "res://Fonts/msgothic.ttc",
            "res://Fonts/msgothic.ttf",
            "res://Fonts/NotoSansMono.ttf",
        };
        foreach (var p in paths)
        {
            if (ResourceLoader.Exists(p))
            {
                loadedFont = GD.Load<FontFile>(p);
                return loadedFont;
            }
        }
        return null;
    }
}
