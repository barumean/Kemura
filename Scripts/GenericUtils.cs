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
    //
    // ConsoleDisplayLineはEmueraアセンブリ内のinternal型なので、これを引数や
    // 戻り値に持つメンバはinternalでなければならない(publicにするとCS0050/
    // CS0051 "一貫性のないアクセシビリティ"になる)。
    // 以前は存在しない`DisplayLine`という型名を使っていたためCS0246だった。
    static EmueraContent? _content;
    internal static void SetContent(EmueraContent? c) => _content = c;

    internal static void AddText(ConsoleDisplayLine line, bool old)
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

    internal static ConsoleDisplayLine? GetText(int lineNo)
        => _content?.GetLine(lineNo);

    public static void RemoveTextCount(int count)
        => _content?.RemoveLines(count);

    // --- Config key MD5 (emuera.config のキー名解決用) ---
    //
    // Emueraエンジン(ConfigData.cs)がこの名前で呼ぶ。Unity版のGenericUtilsに
    // あったが移植されておらず、Assets/削除でCS0117になった。
    // 実装はConfigMapsに置いてある(テーブル側のMD5計算と対になるため)。
    internal static List<string> CalcMd5ListForConfig(byte[] data)
        => ConfigMaps.CalcMd5ListForConfig(data);

    // --- Sprite/texture rect helpers ---
    public static Rect2 ToGodotRect(uEmuera.Drawing.Rectangle src, float texW, float texH)
    {
        if (texW <= 0f || texH <= 0f)
            return new Rect2();
        return new Rect2(
            src.X / texW,
            src.Y / texH,
            src.Width / texW,
            src.Height / texH
        );
    }
}
