using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using MinorShift.Emuera.GameView;

public static class GenericUtils
{
    // --- Logging ---
    public static void Info(object content) => GD.Print(content);
    public static void Warn(object content) => GD.PushWarning(content?.ToString() ?? "");
    public static void Error(object content) => GD.PushError(content?.ToString() ?? "");

    // --- Path helpers ---
    public static string GetFilename(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return Path.GetFileName(path);
    }

    public static string GetBaseDir()
    {
#if GODOT_ANDROID
        return "/storage/emulated/0/emuera/";
#else
        return OS.GetExecutablePath().GetBaseDir().PathJoin("emuera") + "/";
#endif
    }

    // --- Display helpers (delegated to EmueraContent) ---
    static EmueraContent? _content;
    public static void SetContent(EmueraContent c) => _content = c;

    public static void AddText(DisplayLine line, bool old)
        => _content?.AddLine(line, old);

    public static void ClearText()
        => _content?.Clear();

    public static void TextUpdate()
        => _content?.UpdateDisplay();

    public static void SetBackgroundColor(uEmuera.Drawing.Color c)
        => _content?.SetBackgroundColor(c);

    public static void ShowIsInProcess(bool show)
        => _content?.ShowIsInProcess(show);

    public static void SetLastButtonGeneration(int gen)
        => _content?.SetLastButtonGeneration(gen);

    public static int GetTextMaxLineNo()
        => _content?.GetMaxLineNo() ?? 0;

    public static int GetTextMinLineNo()
        => _content?.GetMinLineNo() ?? 0;

    public static DisplayLine? GetText(int lineNo)
        => _content?.GetLine(lineNo);

    public static void RemoveTextCount(int count)
        => _content?.RemoveLines(count);

    // --- Sprite/texture rect helpers ---
    public static Rect2 ToGodotRect(uEmuera.Drawing.Rectangle src, float texW, float texH)
    {
        return new Rect2(
            src.X / texW,
            src.Y / texH,
            src.Width / texW,
            src.Height / texH
        );
    }
}
