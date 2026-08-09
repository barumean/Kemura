using System;
using System.IO;
using System.Text;

/// <summary>
/// era 게임 파일의 문자 인코딩을 파일 단위로 판정한다.
///
/// <b>왜 필요한가</b> — era 게임의 ERB/CSV 는 대부분 SHIFT-JIS(코드페이지 932)로
/// 작성돼 있다. 원본 Emuera 는 932 로 읽는다. 그런데 uEmuera(Unity 포트)는
/// <c>Config.Encode</c> 를 <c>Encoding.UTF8</c> 로 하드코딩하고 932 줄을 주석
/// 처리해 버렸다. 그래서 SHIFT-JIS 스크립트를 UTF-8 로 디코드하게 되고, 모든
/// 일본어 식별자가 U+FFFD(<c>�</c>)로 깨진다. 로그에 남는 증상은 이렇다.
///
/// <code>
///   "ABL:���t����"은(는) 해석할 수 없는 식별자입니다
///   치환할 수 없는 기호[[�L�X��]]
///   지정된 함수명 "@�摜����"은(는) 존재하지 않습니다
/// </code>
///
/// 즉 게임 파일이 잘못된 게 아니고 <b>읽는 쪽이 틀렸다.</b> U+FFFD 는 디코더가
/// 만들어내는 문자이므로, 이 기호가 보이면 원인은 항상 읽기 측이다.
///
/// <b>왜 932 로 고정하지 않는가</b> — UTF-8 로 저장된 era 게임도 있고, 사용자가
/// 직접 만든 파일은 UTF-8 일 가능성이 높다. 932 로 고정하면 그쪽이 깨진다.
/// 그래서 파일마다 판정한다.
///
/// <b>판정 방법</b> — BOM → 엄격한 UTF-8 디코드 시도 → 실패하면 SHIFT-JIS.
/// 일본어 SHIFT-JIS 는 거의 확실하게 유효한 UTF-8 이 아니다(2바이트 문자의
/// 뒷바이트가 0x40~0x7E 범위에 흔히 오는데, UTF-8 계속 바이트는 0x80~0xBF
/// 여야 한다). 반대로 UTF-8 파일은 항상 엄격한 디코드를 통과하므로 SHIFT-JIS
/// 로 오판할 일이 없다. 순수 ASCII 는 두 인코딩이 동일하므로 어느 쪽이든 같다.
/// </summary>
internal static class EraEncoding
{
    /// <summary>판정용으로 앞에서 읽는 최대 바이트 수.</summary>
    const int SniffBytes = 64 * 1024;

    /// <summary>깨진 문자를 만들지 않고 예외를 던지는 UTF-8 디코더(판정 전용).</summary>
    static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

    /// <summary>SHIFT-JIS(932). 사용할 수 없으면 UTF-8 로 대체된다.</summary>
    internal static Encoding ShiftJis { get; }

    /// <summary>932 를 실제로 확보했는지. false 면 SHIFT-JIS 게임이 깨진다.</summary>
    internal static bool ShiftJisAvailable { get; }

    internal static int Utf8Count;
    internal static int ShiftJisCount;

    static EraEncoding()
    {
        // .NET Core 계열에는 코드페이지 인코딩이 기본 포함되지 않는다.
        // 이 등록 없이 GetEncoding(932) 을 부르면 예외가 난다.
        // (kemura.csproj 의 System.Text.Encoding.CodePages 참조가 짝이다.)
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch (Exception e)
        {
            Godot.GD.PushWarning($"코드페이지 인코딩 공급자를 등록할 수 없습니다: {e.Message}");
        }

        Encoding sjis;
        bool ok;
        try
        {
            sjis = Encoding.GetEncoding(932);
            ok = true;
        }
        catch (Exception e)
        {
            // 여기 오면 SHIFT-JIS 게임은 정상적으로 읽을 수 없다. 조용히
            // 넘어가면 "글자가 깨진다"는 증상만 남고 원인을 찾을 수 없으므로
            // 반드시 남긴다.
            sjis = Encoding.UTF8;
            ok = false;
            Godot.GD.PushWarning(
                "SHIFT-JIS(932) 를 사용할 수 없습니다. SHIFT-JIS 로 작성된 게임의 " +
                $"일본어가 깨집니다: {e.Message}");
        }
        ShiftJis = sjis;
        ShiftJisAvailable = ok;
    }

    /// <summary>진단용 한 줄 요약.</summary>
    internal static string Summary =>
        $"인코딩 판정: UTF-8 {Utf8Count}건, SHIFT-JIS {ShiftJisCount}건" +
        (ShiftJisAvailable ? "" : " (경고: 932 사용 불가)");

    internal static void ResetCounters()
    {
        Utf8Count = 0;
        ShiftJisCount = 0;
        loggedSjis = false;
        loggedUtf8 = false;
    }

    /// <summary>경로로 판정한다. 읽을 수 없으면 UTF-8.</summary>
    internal static Encoding Detect(string path)
    {
        try
        {
            using var fs = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Detect(fs);
        }
        catch
        {
            // 호출부가 곧 같은 파일을 열다가 제대로 실패할 것이다.
            // 여기서 예외를 올리면 원인이 인코딩 판정처럼 보여 오해를 부른다.
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// 열린 스트림으로 판정하고 <b>위치를 원래대로 되돌린다.</b>
    /// 같은 스트림을 그대로 StreamReader 에 넘길 수 있다.
    /// </summary>
    internal static Encoding Detect(Stream? stream)
    {
        if (stream == null)
            return Encoding.UTF8;
        long origin = stream.CanSeek ? stream.Position : 0;
        try
        {
            var buf = new byte[SniffBytes];
            int len = 0;
            while (len < buf.Length)
            {
                // Read 는 요청보다 적게 돌려줄 수 있다. EOF 만이 0 이다.
                int n = stream.Read(buf, len, buf.Length - len);
                if (n <= 0) break;
                len += n;
            }
            return Detect(buf, len, complete: len < buf.Length);
        }
        catch
        {
            return Encoding.UTF8;
        }
        finally
        {
            if (stream.CanSeek)
                stream.Seek(origin, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// 바이트로 판정한다.
    /// </summary>
    /// <param name="complete">
    /// buf 가 파일 전체인지. false 면 끝이 잘린 UTF-8 시퀀스일 수 있으므로
    /// 뒤쪽 몇 바이트를 버린다. 이 처리를 빼면 큰 UTF-8 파일이 잘린 위치에
    /// 따라 SHIFT-JIS 로 오판될 수 있다.
    /// </param>
    internal static Encoding Detect(byte[]? buf, int len, bool complete)
    {
        if (buf == null || len <= 0)
            return Count(Encoding.UTF8);

        // BOM 이 있으면 그것이 정답이다.
        if (len >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF)
            return Count(Encoding.UTF8);
        if (len >= 2 && buf[0] == 0xFF && buf[1] == 0xFE)
            return Count(Encoding.Unicode);
        if (len >= 2 && buf[0] == 0xFE && buf[1] == 0xFF)
            return Count(Encoding.BigEndianUnicode);

        if (!complete)
        {
            // 최대 4바이트까지 뒤에서 버려 잘린 시퀀스를 없앤다.
            // 계속 바이트(0x80~0xBF)를 지나 선두 바이트(>=0xC0)를 만나면
            // 그것까지 버리고 멈춘다. 완전한 시퀀스를 버리는 경우가 있지만
            // 판정에는 영향이 없다.
            int cut = len;
            for (int i = 0; i < 4 && cut > 0; i++)
            {
                byte b = buf[cut - 1];
                if (b < 0x80)
                    break;          // ASCII 경계는 안전하다
                cut--;
                if (b >= 0xC0)
                    break;          // 선두 바이트를 버렸다
            }
            len = cut;
            if (len <= 0)
                return Count(Encoding.UTF8);
        }

        try
        {
            StrictUtf8.GetString(buf, 0, len);
            // 순수 ASCII 도 여기로 온다. 두 인코딩의 결과가 같으므로 무해하다.
            return Count(Encoding.UTF8);
        }
        catch (DecoderFallbackException)
        {
            return Count(ShiftJis);
        }
    }

    static bool loggedSjis;
    static bool loggedUtf8;

    static Encoding Count(Encoding e)
    {
        if (ReferenceEquals(e, ShiftJis) && ShiftJisAvailable)
        {
            ShiftJisCount++;
            // 인코딩별로 한 번만 남긴다. 파일마다 찍으면 수천 줄이 된다.
            // 한 줄이라도 남아 있으면 "글자가 깨진다"는 보고를 받았을 때
            // 판정이 동작했는지부터 확인할 수 있다.
            if (!loggedSjis)
            {
                loggedSjis = true;
                Godot.GD.Print("[EraEncoding] SHIFT-JIS 파일을 발견해 932 로 읽습니다.");
            }
        }
        else
        {
            Utf8Count++;
            if (!loggedUtf8)
            {
                loggedUtf8 = true;
                Godot.GD.Print("[EraEncoding] UTF-8 파일을 발견해 UTF-8 로 읽습니다.");
            }
        }
        return e;
    }
}
