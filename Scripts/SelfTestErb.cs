using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using MinorShift.Emuera;

/// <summary>
/// era basic 언어 자체를 <b>실제로 실행해서</b> 검증한다.
///
/// 왜 필요한가: 지금까지의 자기 검증은 C# 헬퍼(PathResolver, EmMapStore 등)만
/// 봤고 ERB 를 한 줄도 실행하지 않았다. 언어 의미가 맞는지는 소스를 읽어
/// 판단했는데, 이 세션에서 소스 읽기로 한 판단이 여러 번 틀렸다.
///
/// 검증 대상은 eratoho ERB 매뉴얼이 <b>기대 출력을 명시한</b> 예제들이다.
/// https://evilmask.gitlab.io/emuera.em.doc/manual/eratohowiki-ERBmanual.html
/// 특히 매뉴얼이 "주의"로 강조한 것들:
///   - REPEAT 의 COUNT 는 0..n-1
///   - CONTINUE / BREAK
///   - FOR 의 시작·끝·증분
///   - WHILE 은 판정 후 실행, DO-LOOP 은 최소 1회 실행
///   - DO-LOOP 안의 CONTINUE 는 DO 가 아니라 LOOP 로 간다(무한루프 아님)
///   - LOCAL / ARG 는 호출 시 초기화되지 않고 재사용된다
///   - SIF 다음의 주석 행은 무시된다(eramaker 와 다른 Emuera 동작)
///   - 삼항연산자, SELECTCASE 의 TO / IS
/// </summary>
internal static class SelfTestErb
{
    /// <summary>실패 개수. 0이면 전부 통과.</summary>
    internal static int Run(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "kemura_erbtest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Build(root);
            var output = RunGame(root);
            if (output == null)
            {
                check(false, "ERB 실행: 엔진을 구동하지 못했습니다");
                return 1;
            }
            return Verify(output, check);
        }
        catch (Exception e)
        {
            check(false, $"ERB 실행 중 예외: {e.GetType().Name}: {e.Message}");
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { /* 임시 폴더 정리 실패는 무해 */ }
        }
    }

    // ------------------------------------------------------------------
    // 게임 만들기
    // ------------------------------------------------------------------

    /// <summary>
    /// 매뉴얼 예제를 그대로 담은 최소 게임. 출력마다 "KEY=값" 형태로 찍어
    /// 파싱하기 쉽게 한다.
    /// </summary>
    static void Build(string root)
    {
        var erb = Path.Combine(root, "ERB");
        var csv = Path.Combine(root, "CSV");
        Directory.CreateDirectory(erb);
        Directory.CreateDirectory(csv);

        // GAMEBASE 없이도 동작해야 하지만, 타이틀 표시를 막기 위해 최소한만 둔다.
        File.WriteAllText(Path.Combine(csv, "GAMEBASE.CSV"),
            "コード,777\nタイトル,ErbSelfTest\n");

        var sb = new System.Text.StringBuilder();
        sb.Append(@"
@SYSTEM_TITLE
	CALL T_REPEAT
	CALL T_FOR
	CALL T_WHILE_DO
	CALL T_TERNARY
	CALL T_SELECTCASE
	CALL T_SIF_COMMENT
	LOCAL = 0
	CALL T_LOCAL_OUTER
	CALL T_ARG_RECURSE, 0
	CALL T_TIMES
	CALL T_DT_SELECT
	CALL T_XML_GET
	CALL T_REGEXP
	CALL T_GETVAR
	CALL T_METH
	CALL T_SERIAL
	PRINTL DONE
	QUIT

; --- REPEAT: COUNT 는 0..n-1, CONTINUE / BREAK ---
@T_REPEAT
	PRINTFORM CONT=
	REPEAT 10
		A = COUNT
		IF A == 5
			CONTINUE
		ENDIF
		PRINTFORM {A}:
	REND
	PRINTL
	PRINTFORM BRK=
	REPEAT 10
		A = COUNT
		IF A == 5
			BREAK
		ENDIF
		PRINTFORM {A}:
	REND
	PRINTL

; --- FOR: 시작/끝/증분 ---
@T_FOR
	PRINTFORM FOR38=
	FOR LOCAL, 3, 8
		PRINTFORM {LOCAL}:
	NEXT
	PRINTL
	PRINTFORM FORSTEP=
	FOR LOCAL, 0, 10, 2
		PRINTFORM {LOCAL}:
	NEXT
	PRINTL

; --- WHILE 은 판정 후 실행, DO-LOOP 은 최소 1회 ---
@T_WHILE_DO
	LOCAL = 0
	PRINTFORM WHILE0=
	WHILE LOCAL < 0
		PRINTFORM X
	WEND
	PRINTL (end)
	PRINTFORM DO0=
	DO
		PRINTFORM O
	LOOP LOCAL < 0
	PRINTL (end)
	; 매뉴얼: DO 안의 CONTINUE 는 LOOP 로 간다. 무한루프가 되면 안 된다.
	DO
		CONTINUE
	LOOP 0
	PRINTL DOCONT=ok

; --- 삼항연산자 ---
@T_TERNARY
	LOCAL = 5
	PRINTFORML TERN={LOCAL >= 3 ? 1 # 0}{LOCAL >= 9 ? 1 # 0}
	PRINTFORML TERNS=\@ LOCAL >= 3 ? yes # no \@

; --- SELECTCASE 의 TO / IS / 복수 지정 ---
@T_SELECTCASE
	PRINTFORM SEL=
	FOR LOCAL, 0, 6
		SELECTCASE LOCAL
			CASE 1, 2
				PRINTFORM a
			CASE 3 TO 4
				PRINTFORM b
			CASE IS >= 5
				PRINTFORM c
			CASEELSE
				PRINTFORM z
		ENDSELECT
	NEXT
	PRINTL

; --- SIF 다음 주석 행은 무시된다(Emuera 동작) ---
@T_SIF_COMMENT
	LOCAL = 0
	SIF LOCAL == 1
	; 이 주석은 무시되어야 한다. eramaker 라면 아래 행이 실행돼버린다.
		PRINTFORM BAD
	PRINTL SIFCOMMENT=ok

; --- LOCAL 은 함수마다 별개 ---
@T_LOCAL_OUTER
	LOCAL = 123
	CALL T_LOCAL_INNER
	PRINTFORML LOCALSCOPE={LOCAL}

@T_LOCAL_INNER
	LOCAL = 567
	RETURN

; --- ARG 는 호출 시 초기화되지 않는다(매뉴얼이 강조한 규약) ---
;     CALL T_ARG_RECURSE, 0 으로 부르면 10이 10번 찍힌다.
@T_ARG_RECURSE, ARG
	SIF ARG >= 10
		RETURN
	CALL T_ARG_RECURSE, ARG + 1
	PRINTFORML ARGREC{ARG}

; --- TIMES: 소수 곱셈 ---
@T_TIMES
	LOCAL = 1000
	TIMES LOCAL, 1.5
	PRINTL
	PRINTFORML TIMES={LOCAL}

; --- DT_SELECT: 4번째 인수(출력 배열) ---
; 규격 문서의 예제를 줄인 것. 예전에는 3인수까지만 받아서
; ""인수가 너무 많습니다"" 로 실패했다.
@T_DT_SELECT
	#DIM L_IDX, 10
	#DIM L_CNT
	DT_CREATE ""db""
	DT_COLUMN_ADD ""db"", ""age"", ""int16""
	DT_ROW_ADD ""db"", ""age"", 11
	DT_ROW_ADD ""db"", ""age"", 21
	DT_ROW_ADD ""db"", ""age"", 18
	L_CNT = DT_SELECT(""db"", ""age >= 18"", ""age ASC"", L_IDX)
	PRINTFORML DTSEL={L_CNT}:{L_IDX:0}:{L_IDX:1}

; --- XML_GET: 3번째 인수가 출력 배열인 형태 ---
; 예전에는 3번째를 정수로만 받아 ""3번째 인수가 숫자가 아닙니다"" 로 실패했다.
@T_XML_GET
	#DIMS L_X
	#DIMS L_NODES, 10
	#DIM L_N
	L_X = <a><b>hello</b><b>world</b></a>
	XML_DOCUMENT 0, L_X
	L_N = XML_GET(0, ""/a/b"", L_NODES, 1)
	PRINTFORML XMLARR={L_N}:%L_NODES:0%:%L_NODES:1%

; --- REGEXPMATCH: 출력 인수 형태 ---
; 규격 문서의 예제. 매치 3건, 그룹 2개, 첫 매치는 ple / le.
@T_REGEXP
	#DIM L_GC
	#DIMS L_M, 10
	#DIM L_C
	L_C = REGEXPMATCH(""Apple Banana Car"", "".(.{2})\\b"", L_GC, L_M)
	PRINTFORML REGEXP={L_C}:{L_GC}:%L_M:0%:%L_M:1%

; --- 이름으로 변수 다루기 ---
; EXISTVAR 의 비트: 정수 1, 문자열 2, 상수 4, 2차원 8, 3차원 16.
; 없는 이름은 0 이어야 한다(게임이 존재 확인에 쓴다).
@T_GETVAR
	; #DIM 은 함수 본문 맨 위에만 올 수 있다. 실행문 사이에 두면 그 파일
	; 전체가 해석 실패하고 아무 출력도 나오지 않는다(내가 한 번 그랬다).
	#DIM L_N = 10
	#DIMS L_S = ""local""
	#DIM L_ARR, 5
	#DIMS L_SARR, 5
	#DIM L_IDX = 2
	PRINTFORML GV={GETVAR(""L_N"")}:%GETVARS(""L_S"")%:{EXISTVAR(""L_N"")}:{EXISTVAR(""L_S"")}:{EXISTVAR(""NOPE_XYZ"")}
	SETVAR ""L_N"", 42
	SETVAR ""L_S"", ""set""
	PRINTFORML SV={L_N}:%L_S%

	; 이름에 인덱스가 붙은 요소 참조. 게임이 실제로 이 형태를 쓴다.
	; 인덱스는 리터럴일 수도 있고 변수 이름일 수도 있다.
	L_ARR:2 = 77
	; 문자열 변수에 = 를 쓰면 줄 나머지를 그대로 대입한다. 따옴표까지
	; 값에 들어가므로 식 대입(따옴표 앞에 아포스트로피)을 써야 한다.
	L_SARR:2 '= ""hit""
	PRINTFORML GVIX={GETVAR(""L_ARR:2"")}:{GETVAR(""L_ARR:L_IDX"")}:%GETVARS(""L_SARR:2"")%
	SETVAR ""L_ARR:3"", 88
	PRINTFORML SVIX={L_ARR:3}

	; VARSETEX: 이름으로 배열 채우기. to 위치는 포함하지 않는다.
	VARSETEX ""L_ARR"", 9, 1, 1, 3
	PRINTFORML VSEX={L_ARR:0}:{L_ARR:1}:{L_ARR:2}:{L_ARR:3}
	; 범위를 생략하면 끝까지
	VARSETEX ""L_SARR"", ""z"", 1
	PRINTFORML VSEXS=%L_SARR:0%:%L_SARR:4%

; --- EXISTMETH: #FUNCTION 은 1, #FUNCTIONS 는 2, 없으면 0 ---
@T_METH
	PRINTFORML EM={EXISTMETH(""T_M_INT"")}:{EXISTMETH(""T_M_STR"")}:{EXISTMETH(""NOPE_XYZ"")}

@T_M_INT
#FUNCTION
	RETURNF 1

@T_M_STR
#FUNCTIONS
	RETURNF ""x""

; --- MAP_GETKEYS / MAP_TOXML / MAP_FROMXML / DT_TOXML / DT_FROMXML ---
@T_SERIAL
	#DIMS L_KEYS, 5
	#DIMS L_XML
	#DIMS L_SCHEMA
	#DIMS L_DATA
	#DIM L_CNT
	MAP_CREATE ""m1""
	MAP_SET ""m1"", ""a"", ""1""
	MAP_SET ""m1"", ""b"", ""2""
	PRINTFORML MKEYS=%MAP_GETKEYS(""m1"")%
	L_CNT = 0
	SIF MAP_GETKEYS(""m1"", L_KEYS, 1) == """"
		L_CNT = 1
	PRINTFORML MKEYSA={L_CNT}:%L_KEYS:0%:%L_KEYS:1%

	; 직렬화 후 다른 맵으로 되돌린다
	L_XML '= MAP_TOXML(""m1"")
	MAP_CREATE ""m2""
	PRINTFORML MXML={MAP_FROMXML(""m2"", L_XML)}:%MAP_GET(""m2"", ""a"")%:%MAP_GET(""m2"", ""b"")%

	; DataTable 도 같은 방식으로 왕복시킨다
	DT_CREATE ""t1""
	DT_COLUMN_ADD ""t1"", ""age"", ""int16""
	DT_ROW_ADD ""t1"", ""age"", 7
	L_DATA '= DT_TOXML(""t1"", L_SCHEMA)
	DT_CREATE ""t2""
	PRINTFORML DXML={DT_FROMXML(""t2"", L_SCHEMA, L_DATA)}:{DT_ROW_LENGTH(""t2"")}:{DT_CELL_GET(""t2"", 0, ""age"", 1)}
");
        File.WriteAllText(Path.Combine(erb, "TEST.ERB"),
            sb.ToString().Replace("\r\n", "\n"));
    }

    // ------------------------------------------------------------------
    // 실행
    // ------------------------------------------------------------------

    /// <summary>엔진을 동기적으로 돌리고 출력을 모은다. 실패하면 null.</summary>
    static List<string>? RunGame(string root)
    {
        var parent = Path.GetDirectoryName(root.TrimEnd('/', '\\'));
        var name = Path.GetFileName(root.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return null;

        MinorShift._Library.Sys.SetWorkFolder(parent);
        MinorShift._Library.Sys.SetSourceFolder(name);
        PathResolver.ClearCache();

        var captured = new List<string>();
        GenericUtils.TextCapture = captured;
        try
        {
            // Program.Main -> win.Init() -> EmueraConsole.Initialize() 가
            // CSV/ERB 로드와 @SYSTEM_TITLE 실행까지 동기로 끝낸다.
            Program.Main(Array.Empty<string>());

            // 하지만 화면으로 나가는 출력은 MainWindow.Update() 안에서만
            // GenericUtils.AddText 로 흘러나온다(Window.cs). 평소에는 Godot
            // 메인 루프가 매 프레임 부르지만, 여기서는 루프가 없으므로 직접
            // 펌프해야 한다. 이걸 안 해서 첫 시도의 출력이 완전히 비었다.
            var win = GlobalStatic.MainWindow;
            if (win == null)
            {
                uEmuera.Logger.Warn("SelfTestErb: MainWindow 가 만들어지지 않았습니다");
                return captured;
            }
            // 더 나올 것이 없을 때까지 돌린다. 무한루프를 막기 위해 상한을 둔다.
            int idle = 0;
            for (int i = 0; i < 2000 && idle < 20; i++)
            {
                int before = captured.Count;
                win.Update();
                idle = captured.Count == before ? idle + 1 : 0;
            }
        }
        finally
        {
            GenericUtils.TextCapture = null;
        }
        return captured;
    }

    // ------------------------------------------------------------------
    // 검증
    // ------------------------------------------------------------------

    static int Verify(List<string> output, Action<bool, string> check)
    {
        var all = string.Join("\n", output);
        int failed = 0;

        void Expect(string key, string want)
        {
            var got = Find(output, key);
            bool ok = got == want;
            if (!ok) ++failed;
            check(ok, $"ERB {key}: 기대 '{want}' 실제 '{got ?? "(없음)"}'");
        }

        // 엔진이 끝까지 돌았는지 먼저 본다. 이게 없으면 나머지는 의미가 없다.
        bool done = all.Contains("DONE");
        if (!done) ++failed;
        check(done, "ERB 실행이 끝까지 진행됐다 (DONE)");
        if (!done)
        {
            // 진단을 위해 실제로 무엇이 나왔는지 남긴다.
            if (output.Count == 0)
                GD.Print("[SelfTest] ERB 출력이 하나도 없습니다. "
                    + "게임이 실행되기 전(ERB 로드/해석) 단계에서 실패했다는 뜻입니다. "
                    + "테스트 ERB 자체의 문법 오류일 가능성이 큽니다 "
                    + "(예: #DIM 을 실행문 사이에 두면 파일 전체가 해석 실패한다).");
            else
                GD.Print("[SelfTest] ERB 출력 덤프:\n" + all);
            return failed;
        }

        // 매뉴얼에 기대 출력이 명시된 것들
        Expect("CONT", "0:1:2:3:4:6:7:8:9:");
        Expect("BRK", "0:1:2:3:4:");
        Expect("FOR38", "3:4:5:6:7:");
        Expect("FORSTEP", "0:2:4:6:8:");
        Expect("WHILE0", "(end)");        // 한 번도 실행되지 않는다
        Expect("DO0", "O(end)");          // 최소 1회 실행된다
        Expect("DOCONT", "ok");           // 무한루프가 되지 않는다
        Expect("TERN", "10");
        Expect("TERNS", "yes");
        Expect("SEL", "zaabbc");
        Expect("SIFCOMMENT", "ok");
        Expect("LOCALSCOPE", "123");      // 함수마다 별개
        Expect("TIMES", "1500");
        // 출력(ref) 인수를 받는 형태. 규격 문서의 예제 기댓값이다.
        Expect("DTSEL", "2:2:1");             // age ASC 이므로 18(id2), 21(id1)
        Expect("XMLARR", "2:hello:world");    // outputType 1 = InnerText
        Expect("REGEXP", "3:2:ple:le");
        // 이름으로 변수 다루기
        Expect("GV", "10:local:1:2:0");
        Expect("SV", "42:set");
        // 이름에 인덱스가 붙은 요소 참조 (리터럴 인덱스 / 변수 인덱스)
        Expect("GVIX", "77:77:hit");
        Expect("SVIX", "88");
        // VARSETEX. L_ARR 은 앞에서 [0,0,77,88,0] 이 됐고, from=1 to=3 은
        // 인덱스 1,2 만 채우므로 [0,9,9,88,0] 이 된다(to 는 포함하지 않는다).
        Expect("VSEX", "0:9:9:88");
        Expect("VSEXS", "z:z");
        Expect("EM", "1:2:0");
        // MAP / DataTable 의 키 목록과 직렬화
        Expect("MKEYS", "a,b");
        Expect("MKEYSA", "1:a:b");        // 배열 형태는 빈 문자열을 돌려준다
        Expect("MXML", "1:1:2");          // 왕복 후 값이 보존된다
        Expect("DXML", "1:1:7");

        // ARG 비초기화: ARGREC 이 10만 10번 나와야 한다.
        //
        // 처음에는 PRINTFORM 으로 한 줄에 몰아넣고 ',' 로 쪼개 셌는데,
        // 표시 행에는 폭 제한이 있어 10번째 항목이 "ARG" 로 잘렸다.
        // 그래서 엔진이 9번만 재귀한 것처럼 보였다(엔진은 정상이었다).
        // 이제 PRINTFORML 로 한 항목당 한 줄씩 찍고 줄 단위로 센다.
        int tens = 0, others = 0;
        foreach (var line in output)
        {
            int i = line.IndexOf("ARGREC", StringComparison.Ordinal);
            if (i < 0) continue;
            var v = line.Substring(i + 6).Trim();
            if (v == "10") ++tens; else ++others;
        }
        bool argOk = tens == 10 && others == 0;
        if (!argOk) ++failed;
        check(argOk,
            $"ERB ARG 는 호출 시 초기화되지 않는다: 기대 '10'x10, 실제 10이 {tens}회 / 그 외 {others}회");
        if (!argOk)
        {
            // 하네스의 계수 문제인지 엔진의 재귀 깊이 문제인지 가르려면
            // 실제로 찍힌 원문이 필요하다. 캡처된 줄을 그대로 남긴다.
            foreach (var line in output)
            {
                if (line.Contains("ARGREC"))
                    GD.Print("[ErbTest] ARGREC 원문: " + line);
            }
        }

        return failed;
    }

    /// <summary>"KEY=값" 형태의 값을 찾는다.</summary>
    static string? Find(List<string> output, string key)
    {
        foreach (var line in output)
        {
            int i = line.IndexOf(key + "=", StringComparison.Ordinal);
            if (i < 0) continue;
            return line.Substring(i + key.Length + 1).Trim();
        }
        return null;
    }
}
