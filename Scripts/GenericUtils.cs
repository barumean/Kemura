using Godot;
using System;
using System.IO;
using MinorShift.Emuera.GameView;

/// <summary>
/// Window.cs (uEmuera.Window.MainWindow)와 EmueraContent 간의 브릿지
/// 원본 Unity 버전의 static 유틸리티 메서드 시그니처와 호환 유지
/// </summary>
public static class GenericUtils
{
    // ── 로깅 ────────────────────────────────────────────────────
    public static void Info(object content)  => GD.Print(content);
    public static void Warn(object content)  => GD.PushWarning(content?.ToString() ?? "");
    public static void Error(object content) => GD.PushError(content?.ToString() ?? "");

    // ── 경로 헬퍼 ───────────────────────────────────────────────
    public static string GetFilename(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return Path.GetFileName(path) ?? "";
    }

    // ── EmueraContent 참조 ──────────────────────────────────────
    static EmueraContent? _content;
    public static void SetContent(EmueraContent c) => _content = c;

    // ── Window.cs가 호출하는 디스플레이 브릿지 메서드 ──────────
    // Window.cs:171  GenericUtils.AddText(line, line.LineNo <= prev)
    // 원본은 object 타입이었으나 Godot 버전에서는 ConsoleDisplayLine 직접 사용
    public static void AddText(ConsoleDisplayLine line, bool old)
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

    // Window.cs:153  var tl = GenericUtils.GetText(dis_lineno);
    // 원본 Unity는 object 반환 → ConsoleDisplayLine과 == 비교
    public static ConsoleDisplayLine? GetText(int lineNo)
        => _content?.GetLine(lineNo);

    public static void RemoveTextCount(int count)
        => _content?.RemoveLines(count);
}
