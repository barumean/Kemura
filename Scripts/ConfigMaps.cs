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
                "config変換テーブルを読めませんでした。SHIFT-JISで書かれた " +
                "emuera.config のキーが認識されない可能性があります。");
            return;
        }

        var jisMd5s = CalcMd5List(jisBytes);
        var utf8Lines = SplitLines(utf8Text);
        var utf8CnLines = SplitLines(utf8CnText);

        // 3ファイルは行単位で1対1対応している前提。ずれていたら使わない。
        if (jisMd5s.Count != utf8Lines.Count || utf8CnLines.Count != utf8Lines.Count)
        {
            GD.PushWarning(
                $"config変換テーブルの行数が一致しません " +
                $"(shiftjis={jisMd5s.Count}, utf8={utf8Lines.Count}, zhcn={utf8CnLines.Count})。" +
                "変換を無効にします。");
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
            GD.PushWarning($"ConfigMaps: {resPath} が開けません ({FileAccess.GetOpenError()})");
            return null;
        }
        return f.GetBuffer((long)f.GetLength());
    }

    static string? ReadText(string resPath)
    {
        using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (f == null)
        {
            GD.PushWarning($"ConfigMaps: {resPath} が開けません ({FileAccess.GetOpenError()})");
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

    static string CalcMd5(byte[] data, int offset, int count)
    {
        var hash = MD5.HashData(new ReadOnlySpan<byte>(data, offset, count));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}
