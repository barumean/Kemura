using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// emuera.config のキー名変換テーブルをロードする。
///
/// era系ゲームの emuera.config は SHIFT-JIS で書かれていることが多い。
/// Emueraエンジンは行のMD5から正規化済みUTF-8キー名を引くことで、
/// エンコーディングを判定せずにキーを認識する。
/// そのテーブルを uEmuera.Utils に流し込むのがこのクラスの役目。
///
/// Unity版では MainEntry.LoadConfigMaps() がこれを行っていたが、Godot移植時に
/// 移されず、uEmuera.Utils.SHIFTJIS_to_UTF8() が常にnullを返していた
/// (=SHIFT-JIS製configのキーが一切認識されなかった)。
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

        // 3ファイルは行単位で1対1対応している前提。ずれていたら使わない。
        if (jisMd5s.Count != utf8Lines.Count || utf8CnLines.Count != utf8Lines.Count)
        {
            GD.PushWarning(
                $"config 변환 테이블의 행 수가 일치하지 않습니다 " +
                $"(shiftjis={jisMd5s.Count}, utf8={utf8Lines.Count}, zhcn={utf8CnLines.Count})。" +
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
    /// 改行で区切られた各行のMD5を、行の生バイト列から計算する。
    /// SHIFT-JISのままハッシュを取る必要があるためデコードしてはいけない。
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

            // 改行の連続を飛ばす
            start = end;
            while (start < data.Length && (data[start] == 0x0d || data[start] == 0x0a))
                ++start;
        }
        return md5s;
    }

    /// <summary>
    /// 実際の emuera.config 側のMD5リスト。各行の ':' より前(キー名部分)の
    /// 生バイト列からハッシュを取る。テーブル側(CalcMd5List)はキー名のみの
    /// 行をハッシュするので、両者の値が一致することで対応付けられる。
    ///
    /// 移植元(Unity版 GenericUtils)は ':' を探すループに境界チェックが無く、
    /// ':' を含まない行(コメントや末尾行)で IndexOutOfRangeException になった。
    /// ここでは範囲外を防ぎ、':' の無い行は対応表を引けないのでスキップせず
    /// 行数合わせのために空文字を積む(呼び出し側が行番号で添字を取るため)。
    /// </summary>
    internal static List<string> CalcMd5ListForConfig(byte[] data)
    {
        var md5s = new List<string>();
        if (data == null || data.Length == 0) return md5s;

        int pos = 0;
        while (pos < data.Length)
        {
            // 行末を探す
            int lineEnd = pos;
            while (lineEnd < data.Length && data[lineEnd] != 0x0d && data[lineEnd] != 0x0a)
                ++lineEnd;

            // 行内の最初の ':' を探す
            int colon = pos;
            while (colon < lineEnd && data[colon] != (byte)':')
                ++colon;

            if (colon > pos && colon < lineEnd)
                md5s.Add(CalcMd5(data, pos, colon - pos));
            else
                md5s.Add("");   // ':' 無し(空行・コメント等)。行番号との対応を保つ

            if (lineEnd >= data.Length)
                break;

            // 改行を「1つだけ」消費する。StreamReader.ReadLine() は空行も
            // 1行として返すため、連続改行をまとめて飛ばすと添字がずれる。
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
