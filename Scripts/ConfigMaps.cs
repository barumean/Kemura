using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// emuera.config 의 키 이름 변환 테이블을 로드한다.
///
/// era 계열 게임의 emuera.config 는 SHIFT-JIS로 작성된 경우가 많다.
/// Emuera 엔진은 각 행의 MD5로 정규화된 UTF-8 키 이름을 조회함으로써
/// 인코딩을 판별하지 않고도 키를 인식한다.
/// 그 테이블을 uEmuera.Utils 에 주입하는 것이 이 클래스의 역할이다.
///
/// Unity 판에서는 MainEntry.LoadConfigMaps() 가 담당했으나 Godot 이식 때
/// 옮겨지지 않아 uEmuera.Utils.SHIFTJIS_to_UTF8() 이 항상 null을 반환했다
/// (= SHIFT-JIS로 만든 config의 키가 전혀 인식되지 않았다).
/// </summary>
internal static class ConfigMaps
{
    const string ShiftJisPath = "res://Text/emuera_config_shiftjis.bin";
    const string Utf8Path = "res://Text/emuera_config_utf8.txt";
    const string Utf8ZhCnPath = "res://Text/emuera_config_utf8_zhcn.txt";

    static bool loaded;

    internal static void Load()
    {
        if (loaded) return;
        loaded = true;

        var jisBytes = ReadBytes(ShiftJisPath);
        var utf8Text = ReadText(Utf8Path);
        var utf8CnText = ReadText(Utf8ZhCnPath);

        if (jisBytes == null || utf8Text == null || utf8CnText == null)
        {
            GD.PushWarning(
                "config 변환 테이블을 읽을 수 없습니다. SHIFT-JIS로 작성된 " +
                "emuera.config 의 키가 인식되지 않을 수 있습니다.");
            return;
        }

        var jisMd5s = CalcMd5List(jisBytes);
        var utf8Lines = SplitLines(utf8Text);
        var utf8CnLines = SplitLines(utf8CnText);

        // 세 파일은 행 단위로 1:1 대응한다는 전제. 어긋나면 사용하지 않는다.
        if (jisMd5s.Count != utf8Lines.Count || utf8CnLines.Count != utf8Lines.Count)
        {
            GD.PushWarning(
                $"config 변환 테이블의 행 수가 일치하지 않습니다 " +
                $"(shiftjis={jisMd5s.Count}, utf8={utf8Lines.Count}, zhcn={utf8CnLines.Count}). " +
                "변환을 비활성화합니다.");
            return;
        }

        var jisMap = new Dictionary<string, string>(jisMd5s.Count);
        for (int i = 0; i < jisMd5s.Count; ++i)
            jisMap[jisMd5s[i]] = utf8Lines[i];

        var cnMap = new Dictionary<string, string>(utf8CnLines.Count);
        for (int i = 0; i < utf8CnLines.Count; ++i)
            cnMap[utf8CnLines[i]] = utf8Lines[i];

        uEmuera.Utils.SetSHIFTJIS_to_UTF8Dict(jisMap);
        uEmuera.Utils.SetUTF8ZHCN_to_UTF8Dict(cnMap);
        GD.Print($"ConfigMaps: {jisMap.Count} entries loaded");
    }

    static byte[]? ReadBytes(string resPath)
    {
        using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (f == null)
        {
            GD.PushWarning($"ConfigMaps: {resPath} 를 열 수 없습니다 ({FileAccess.GetOpenError()})");
            return null;
        }
        return f.GetBuffer((long)f.GetLength());
    }

    static string? ReadText(string resPath)
    {
        using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (f == null)
        {
            GD.PushWarning($"ConfigMaps: {resPath} 를 열 수 없습니다 ({FileAccess.GetOpenError()})");
            return null;
        }
        return f.GetAsText();
    }

    static List<string> SplitLines(string text)
    {
        var result = new List<string>();
        foreach (var line in text.Split('\r', '\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            result.Add(line);
        }
        return result;
    }

    /// <summary>
    /// 개행으로 구분된 각 행의 MD5를 행의 원본 바이트열에서 계산한다.
    /// SHIFT-JIS 상태로 해시를 구해야 하므로 디코딩하면 안 된다.
    /// </summary>
    static List<string> CalcMd5List(byte[] data)
    {
        var md5s = new List<string>();
        if (data.Length == 0) return md5s;

        int start = 0;
        while (start < data.Length)
        {
            int end = start;
            while (end < data.Length && data[end] != 0x0d && data[end] != 0x0a)
                ++end;

            if (end > start)
                md5s.Add(CalcMd5(data, start, end - start));

            // 연속된 개행을 건너뛴다
            start = end;
            while (start < data.Length && (data[start] == 0x0d || data[start] == 0x0a))
                ++start;
        }
        return md5s;
    }

    /// <summary>
    /// 실제 emuera.config 쪽의 MD5 목록. 각 행의 ':' 이전(키 이름 부분)의
    /// 원본 바이트열에서 해시를 구한다. 테이블 쪽(CalcMd5List)은 키 이름만 있는
    /// 행을 해시하므로, 두 값이 일치하는 것으로 대응된다.
    ///
    /// 이식 원본(Unity 판 GenericUtils)은 ':' 탐색 루프에 경계 검사가 없어
    /// ':' 이 없는 행(주석·마지막 행)에서 IndexOutOfRangeException 이 났다.
    /// 여기서는 범위를 벗어나지 않게 하고, ':' 이 없는 행은 대응표를 못 찾으므로
    /// 건너뛰지 않고 행 수를 맞추기 위해 빈 문자열을 넣는다(호출부가 행 번호로 인덱싱).
    /// </summary>
    internal static List<string> CalcMd5ListForConfig(byte[] data)
    {
        var md5s = new List<string>();
        if (data == null || data.Length == 0) return md5s;

        int pos = 0;
        while (pos < data.Length)
        {
            // 행 끝을 찾는다
            int lineEnd = pos;
            while (lineEnd < data.Length && data[lineEnd] != 0x0d && data[lineEnd] != 0x0a)
                ++lineEnd;

            // 행 안의 첫 ':' 를 찾는다
            int colon = pos;
            while (colon < lineEnd && data[colon] != (byte)':')
                ++colon;

            if (colon > pos && colon < lineEnd)
                md5s.Add(CalcMd5(data, pos, colon - pos));
            else
                md5s.Add("");   // ':' 없음(빈 줄·주석 등). 행 번호 대응을 유지한다

            if (lineEnd >= data.Length)
                break;

            // 개행을 '하나만' 소비한다. StreamReader.ReadLine() 은 빈 줄도
            // 한 행으로 반환하므로, 연속 개행을 한꺼번에 건너뛰면 인덱스가 밀린다.
            if (data[lineEnd] == 0x0d && lineEnd + 1 < data.Length && data[lineEnd + 1] == 0x0a)
                pos = lineEnd + 2;      // CRLF
            else
                pos = lineEnd + 1;      // CR or LF
        }
        return md5s;
    }

    static string CalcMd5(byte[] data, int offset, int count)
    {
        var hash = MD5.HashData(new ReadOnlySpan<byte>(data, offset, count));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}
