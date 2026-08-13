using System;
using System.IO;
using System.Text;

/// <summary>
/// 진단 로그. 실기에서만 드러나는 문제를 원인까지 좁히기 위한 것이다.
///
/// <b>왜 필요한가</b> — 게임이 잘못 동작하는데 <c>emuera.log</c> 에 경고가
/// 한 줄도 없는 상황이 실제로 있었다. "TALENT_RACE_MAP 이 없다"고 게임이
/// THROW 하는데, 파싱은 전부 성공했으니 로그에는 아무 단서가 없다.
/// 그러면 남는 방법이 추측뿐이고, 실제로 가설 몇 개를 틀렸다.
///
/// 게임 쪽에는 이미 단서가 있었다. era 게임들은 <c>DEBUGPRINT</c> 계열로
/// 자체 진단을 찍는다(예: eratoho 의 <c>@MAKE_TALENT_MAP</c> 은 만든 맵을
/// 한 줄씩 출력한다). 그런데 이 명령은 <c>DEBUG_FUNC</c> 플래그가 붙어 있어
/// <c>Program.DebugMode</c> 가 아니면 <b>실행 자체가 건너뛰어진다</b>. 즉
/// 게임이 남긴 단서를 우리가 버리고 있었다.
///
/// 그래서 이 파일이 하는 일은 두 가지다.
///  1. <c>DEBUGPRINT</c> 계열의 출력을 파일로 받는다
///  2. 이름 기반 저장소(MAP 등)의 생성·삭제를 기록한다
///
/// <c>Program.DebugMode</c> 는 건드리지 않는다. 그걸 켜면 <c>ASSERT</c> 가
/// 살아나 게임을 멈출 수 있고, <c>;#;</c> 주석이 실행되기 시작해 게임 동작
/// 자체가 달라진다. 진단이 관측 대상을 바꾸면 안 된다.
///
/// 파일은 앱 전용 외부 경로에 쓴다. 어떤 Android 버전에서도 권한 없이 쓸 수
/// 있어 사용자가 확실히 꺼낼 수 있다.
/// </summary>
internal static class EmDiag
{
    /// <summary>파일이 무한히 커지지 않게 하는 상한.</summary>
    const long MaxBytes = 1L * 1024 * 1024;

    /// <summary>저장소 조작 기록의 상한. 게임이 루프에서 만들면 폭발한다.</summary>
    const int MaxStoreLines = 300;

    static readonly object gate = new();
    static StreamWriter? writer;
    static bool tried;
    static long written;
    static int storeLines;

    /// <summary>진단 로그를 쓰는 중인지.</summary>
    internal static bool Enabled => Open() != null;

    /// <summary>실제 파일 경로. 사용자에게 알려주기 위해 남긴다.</summary>
    internal static string Path { get; private set; } = "";

    static StreamWriter? Open()
    {
        lock (gate)
        {
            if (tried)
                return writer;
            tried = true;
            try
            {
                var dir = Settings.DiagDir;
                Directory.CreateDirectory(dir);
                Path = System.IO.Path.Combine(dir, "kemura_diag.log");
                // 실행마다 새로 쓴다. 이어 붙이면 어느 실행의 것인지 알 수 없다.
                writer = new StreamWriter(Path, false, new UTF8Encoding(false))
                {
                    AutoFlush = true,   // 게임이 멈춰도 남아 있어야 한다
                };
                writer.WriteLine("=== Kemura 진단 로그 ===");
                writer.WriteLine($"{AppInfo.NameWithVersion}");
            }
            catch (Exception e)
            {
                writer = null;
                Godot.GD.PushWarning($"진단 로그를 만들 수 없습니다: {e.Message}");
            }
            return writer;
        }
    }

    /// <summary>한 줄 남긴다.</summary>
    internal static void Line(string tag, string message)
    {
        var w = Open();
        if (w == null || written > MaxBytes)
            return;
        lock (gate)
        {
            try
            {
                var s = $"[{tag}] {message}";
                w.WriteLine(s);
                written += s.Length + 1;
            }
            catch
            {
                // 진단이 실패해도 게임은 계속 돌아야 한다.
            }
        }
    }

    /// <summary>DEBUGPRINT 계열의 출력(줄바꿈 없이 이어짐).</summary>
    internal static void DebugWrite(string? text)
    {
        var w = Open();
        if (w == null || written > MaxBytes)
            return;
        lock (gate)
        {
            try
            {
                w.Write(text ?? "");
                written += (text?.Length ?? 0);
            }
            catch { }
        }
    }

    internal static void DebugNewLine()
    {
        var w = Open();
        if (w == null || written > MaxBytes)
            return;
        lock (gate)
        {
            try { w.WriteLine(); written++; } catch { }
        }
    }

    /// <summary>
    /// 이름 기반 저장소의 생성·삭제. 이름이 어떻게 만들어졌는지가 핵심이라
    /// 해석된 문자열을 그대로 남긴다.
    /// </summary>
    internal static void Store(string op, string name, long result)
    {
        if (storeLines >= MaxStoreLines)
            return;
        storeLines++;
        Line("Store", $"{op} \"{name}\" -> {result}");
        if (storeLines == MaxStoreLines)
            Line("Store", "(이후 생략)");
    }
}
