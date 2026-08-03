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

    /// <summary>
    /// 게임 폴더의 루트. 플랫폼별 기본값과 사용자 설정의
    /// 해석은 Settings 한 곳으로 모아뒀다(이전에는 여기와 FirstWindow/EmueraMain
    /// 3곳에 같은 분기가 중복돼 있었다).
    /// </summary>
    public static string GetBaseDir() => Settings.EffectiveGameRoot;

    // --- Display helpers (delegated to EmueraContent) ---
    //
    // ConsoleDisplayLine 은 Emuera 어셈블리 내의 internal 타입이므로, 이를 인자나
    // 반환값으로 갖는 멤버는 internal 이어야 한다(public 으로 하면 CS0050/
    // CS0051 "일관성 없는 접근성" 오류가 된다).
    // 이전에는 존재하지 않는 `DisplayLine` 이라는 타입명을 써서 CS0246 이었다.
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

    // --- Config key MD5 (emuera.config 의 키 이름 해석용) ---
    //
    // Emuera 엔진(ConfigData.cs)이 이 이름으로 호출한다. Unity 판 GenericUtils에
    // 있었으나 이식되지 않아, Assets/ 삭제로 CS0117 이 됐다.
    // 구현은 ConfigMaps 에 있다(테이블 쪽 MD5 계산과 쌍을 이루므로).
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
