using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.GameData;
using MinorShift.Emuera.GameData.Variable;

/// <summary>
/// 대소문자 파일 해석에 대한 자기 검증.
///
/// 왜 필요한가: "PC에서는 되는데 실기에서만 게임이 열리지 않는다" 는 증상의
/// 원인이 파일명 대소문자였는데, CI는 Linux(=대소문자 구분)에서 도는데도
/// 초록이었다. 컴파일과 씬 임포트만 확인했기 때문이다.
/// 실제 파일을 만들어 검색이 되는지 확인하지 않으면 같은 회귀를 또 놓친다.
///
/// 실행: godot --headless -- --kemura-selftest
/// 전부 통과하면 종료 코드 0, 하나라도 실패하면 1.
/// </summary>
internal static class SelfTest
{
    internal const string Flag = "--kemura-selftest";

    /// <summary>명령줄에 자기 검증 플래그가 있는지.</summary>
    internal static bool Requested()
    {
        foreach (var a in OS.GetCmdlineArgs())
            if (a == Flag) return true;
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a == Flag) return true;
        return false;
    }

    static int failures;

    static void Check(bool ok, string what)
    {
        if (ok)
        {
            GD.Print($"[SelfTest] PASS  {what}");
        }
        else
        {
            ++failures;
            GD.Print($"[SelfTest] FAIL  {what}");
        }
    }

    /// <summary>모두 통과하면 0, 실패가 있으면 1.</summary>
    internal static int Run()
    {
        failures = 0;
        var root = Path.Combine(Path.GetTempPath(), "kemura_selftest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Build(root);
            PathResolver.ClearCache();
            RunChecks(root);
            RunGameBaseChecks(root);
            RunArraySizeChecks();
            RunEmExtensionChecks();
            RunEncodingChecks(root);
            RunAppInfoChecks();
            // ERB 를 실제로 실행해 언어 의미를 검증한다.
            // 엔진 전체를 구동하므로 다른 검사 뒤에 둔다.
            //
            // 한동안은 하네스 자체를 신뢰할 수 없어 실패를 종료 코드에 반영하지
            // 않았다(실제로 하네스 쪽 계수 버그로 엔진이 정상인데 FAIL 이 나온
            // 적이 있다). 15건 전부 통과하는 것을 CI 에서 확인했으므로 이제
            // 합산한다. 여기서 막지 않으면 언어 의미가 깨지는 회귀를 놓친다.
            int erbFailed = SelfTestErb.Run((ok, what) =>
                GD.Print($"[ErbTest] {(ok ? "PASS" : "FAIL")}  {what}"));
            GD.Print(erbFailed == 0
                ? "[ErbTest] ALL PASS"
                : $"[ErbTest] {erbFailed} FAILED");
            failures += erbFailed;
        }
        catch (Exception e)
        {
            ++failures;
            GD.Print($"[SelfTest] FAIL  예외: {e}");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch (Exception e) { GD.Print($"[SelfTest] 정리 실패(무해): {e.Message}"); }
        }

        GD.Print(failures == 0
            ? "[SelfTest] ALL PASS"
            : $"[SelfTest] {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }

    // ----------------------------------------------------------------------
    // 문자 인코딩
    //
    // era 게임의 ERB/CSV 는 대부분 SHIFT-JIS 다. uEmuera 가 Config.Encode 를
    // UTF-8 로 하드코딩해서, SHIFT-JIS 게임의 일본어 식별자가 전부 U+FFFD 로
    // 깨졌다("해석할 수 없는 식별자입니다" 수천 건).
    //
    // 이 검사가 없어서 놓쳤다. 기존 검사는 전부 파일을 UTF-8 로 쓰고 UTF-8 로
    // 읽었으니 통과할 수밖에 없었다. 실제 SHIFT-JIS 바이트를 파일로 써서
    // 엔진 경로로 읽어야 한다.
    // ----------------------------------------------------------------------
    static void RunEncodingChecks(string root)
    {
        Check(EraEncoding.ShiftJisAvailable,
            "SHIFT-JIS(932) 를 사용할 수 있다 (CodePages 공급자 등록)");

        var sjis = EraEncoding.ShiftJis;
        Check(sjis.CodePage == 932,
            $"EraEncoding.ShiftJis 가 932 다 (실제 {sjis.CodePage})");

        // --- 판정 단위 검사 --------------------------------------------------
        // "東方" 를 각 인코딩으로 바이트화해 판정이 맞는지 본다.
        const string jp = "東方紅魔郷";
        var sjisBytes = sjis.GetBytes(jp);
        var utf8Bytes = Encoding.UTF8.GetBytes(jp);

        Check(EraEncoding.Detect(sjisBytes, sjisBytes.Length, true).CodePage == 932,
            "Detect: SHIFT-JIS 바이트를 932 로 판정한다");
        Check(EraEncoding.Detect(utf8Bytes, utf8Bytes.Length, true).CodePage == 65001,
            "Detect: UTF-8 바이트를 UTF-8 로 판정한다");

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var withBom = new byte[bom.Length + utf8Bytes.Length];
        Buffer.BlockCopy(bom, 0, withBom, 0, bom.Length);
        Buffer.BlockCopy(utf8Bytes, 0, withBom, bom.Length, utf8Bytes.Length);
        Check(EraEncoding.Detect(withBom, withBom.Length, true).CodePage == 65001,
            "Detect: UTF-8 BOM");

        var ascii = Encoding.ASCII.GetBytes("@SYSTEM_TITLE\n");
        Check(EraEncoding.Detect(ascii, ascii.Length, true).CodePage == 65001,
            "Detect: 순수 ASCII 는 UTF-8 (두 인코딩이 같으므로 무해)");

        Check(EraEncoding.Detect(Array.Empty<byte>(), 0, true).CodePage == 65001,
            "Detect: 빈 파일은 UTF-8");

        // 앞부분만 읽었을 때 UTF-8 시퀀스가 잘려도 SHIFT-JIS 로 오판하지 않는다.
        // 이 처리를 빼면 큰 UTF-8 파일이 잘린 위치에 따라 깨진다.
        Check(EraEncoding.Detect(utf8Bytes, utf8Bytes.Length - 1, complete: false)
                .CodePage == 65001,
            "Detect: 끝이 잘린 UTF-8 을 SHIFT-JIS 로 오판하지 않는다");

        // --- 엔진 경로 통과 검사 (GAMEBASE.CSV) -----------------------------
        // GameBase.LoadGameBaseCsv 는 EraStreamReader 를 쓴다. 즉 실제 게임이
        // 로드되는 것과 같은 경로다.
        var sjisCsv = Path.Combine(root, "gamebase_sjis.csv");
        File.WriteAllBytes(sjisCsv, sjis.GetBytes(
            "コード,777\nタイトル,東方紅魔郷\n作者,ZUN\n"));

        var gbS = new GameBase();
        bool okS = gbS.LoadGameBaseCsv(sjisCsv);
        Check(okS, "SHIFT-JIS GAMEBASE.CSV 를 로드한다");
        Check(gbS.ScriptUniqueCode == 777,
            $"SHIFT-JIS: 「コード」 지시어를 인식한다 (실제 {gbS.ScriptUniqueCode})");
        Check(gbS.ScriptTitle == "東方紅魔郷",
            $"SHIFT-JIS: 일본어 값이 깨지지 않는다 (실제 '{gbS.ScriptTitle}')");
        Check(gbS.ScriptTitle != null && !gbS.ScriptTitle.Contains('�'),
            "SHIFT-JIS: 제목에 U+FFFD 가 없다");

        // 같은 내용을 UTF-8 로 써도 통과해야 한다(둘 다 지원한다는 확인).
        var utf8Csv = Path.Combine(root, "gamebase_utf8.csv");
        File.WriteAllText(utf8Csv, "コード,778\nタイトル,東方紅魔郷\n",
            new UTF8Encoding(false));
        var gbU = new GameBase();
        Check(gbU.LoadGameBaseCsv(utf8Csv) && gbU.ScriptTitle == "東方紅魔郷",
            $"UTF-8 GAMEBASE.CSV 도 그대로 동작한다 (실제 '{gbU.ScriptTitle}')");

        // --- 엔진 경로 통과 검사 (ERB) --------------------------------------
        // 로그에 나온 증상은 ERB 안의 식별자가 깨지는 것이었다.
        var sjisErb = Path.Combine(root, "sjis_test.erb");
        const string erbLine = "\tABL:調教技術 = 5";
        File.WriteAllBytes(sjisErb, sjis.GetBytes("@陥落状態\n" + erbLine + "\n"));

        using (var er = new MinorShift.Emuera.Sub.EraStreamReader(false))
        {
            Check(er.Open(sjisErb), "SHIFT-JIS ERB 를 EraStreamReader 로 연다");
            var l1 = er.ReadLine();
            var l2 = er.ReadLine();
            Check(l1 == "@陥落状態",
                $"SHIFT-JIS ERB: 함수명이 깨지지 않는다 (실제 '{l1}')");
            Check(l2 == erbLine,
                $"SHIFT-JIS ERB: 변수명이 깨지지 않는다 (실제 '{l2}')");
            Check(l1 != null && !l1.Contains('�'),
                "SHIFT-JIS ERB: U+FFFD 가 없다");
        }
    }

    /// <summary>
    /// 앱 이름·버전이 런타임에 실제로 읽히는지 확인한다.
    ///
    /// 버전은 project.godot 이 원본이고 화면·로그 표기는 AppInfo 가 거기서
    /// 읽는다. 설정 키 이름을 틀리면 조용히 fallback("0.0.0")이 표시되므로,
    /// 값이 비어 있지 않은지가 아니라 fallback 이 아닌지를 본다.
    /// export_presets.cfg 와의 일치는 CI 의 version-consistency 잡이 본다.
    /// </summary>
    static void RunAppInfoChecks()
    {
        Check(AppInfo.Name == "Kemura", $"AppInfo.Name (실제 '{AppInfo.Name}')");
        Check(AppInfo.Version != "0.0.0",
            $"AppInfo.Version 이 project.godot 에서 읽힌다 (실제 '{AppInfo.Version}')");
        Check(System.Text.RegularExpressions.Regex.IsMatch(AppInfo.Version, @"^\d+\.\d+\.\d+$"),
            $"AppInfo.Version 이 MAJOR.MINOR.PATCH 형식이다 (실제 '{AppInfo.Version}')");
        Check(AppInfo.PackageName == "com.kemura.emuera",
            $"AppInfo.PackageName (실제 '{AppInfo.PackageName}')");
        Check(Settings.AppExternalGameRoot.Contains(Settings.PackageName),
            "앱 전용 폴더 경로가 PackageName 을 쓴다");
        Check(AppInfo.NameWithVersion == $"Kemura v{AppInfo.Version}",
            $"AppInfo.NameWithVersion (실제 '{AppInfo.NameWithVersion}')");
    }

    /// <summary>
    /// 실기에서 흔히 보는 배포 형태를 흉내낸다. 엔진이 하드코딩으로 찾는
    /// 이름은 전부 대문자("ERB", "GAMEBASE.CSV", "*.ERB")인데,
    /// 실제 파일은 소문자·혼합 표기다.
    /// </summary>
    static void Build(string root)
    {
        var erb = Path.Combine(root, "erb");          // 엔진은 "ERB" 를 찾는다
        var csv = Path.Combine(root, "Csv");          // 엔진은 "csv"/"CSV" 를 찾는다
        var sub = Path.Combine(erb, "chara");
        Directory.CreateDirectory(sub);
        Directory.CreateDirectory(csv);

        File.WriteAllText(Path.Combine(root, "emuera.config"), ";test\n");
        File.WriteAllText(Path.Combine(erb, "system.erb"), "@SYSTEM_TITLE\n");
        File.WriteAllText(Path.Combine(erb, "shop.Erb"), "@SHOP\n");
        File.WriteAllText(Path.Combine(erb, "train.ERB"), "@TRAIN\n");
        File.WriteAllText(Path.Combine(sub, "deep.erb"), "@DEEP\n");
        File.WriteAllText(Path.Combine(csv, "gamebase.csv"), "코드,0\n");
        File.WriteAllText(Path.Combine(csv, "chara0.csv"), "번호,0\n");
        // 확장자가 비슷하지만 달라서 걸리면 안 되는 것
        File.WriteAllText(Path.Combine(erb, "notes.erbak"), "not a script\n");
    }

    static void RunChecks(string root)
    {
        // --- ResolveFile -----------------------------------------------------
        var csvDirGuess = Path.Combine(root, "csv");                  // 실제는 "Csv"
        var gamebaseGuess = Path.Combine(csvDirGuess, "GAMEBASE.CSV"); // 실제는 "gamebase.csv"

        var gamebase = PathResolver.ResolveFile(gamebaseGuess);
        Check(File.Exists(gamebase),
            $"ResolveFile: 상위 폴더와 파일명 모두 대소문자가 달라도 찾는다 ({gamebase})");

        var cfg = PathResolver.ResolveFile(Path.Combine(root, "EMUERA.CONFIG"));
        Check(File.Exists(cfg), "ResolveFile: emuera.config");

        var exact = Path.Combine(root, "emuera.config");
        Check(PathResolver.ResolveFile(exact) == exact,
            "ResolveFile: 정확한 경로는 그대로 반환한다(빠른 경로)");

        var missing = Path.Combine(root, "no_such_file.csv");
        Check(PathResolver.ResolveFile(missing) == missing,
            "ResolveFile: 없는 파일은 원본을 그대로 반환해 호출부의 오류 처리를 살린다");

        // --- ResolveDirectory ------------------------------------------------
        var erbDir = PathResolver.ResolveDirectory(Path.Combine(root, "ERB"));
        Check(Directory.Exists(erbDir), $"ResolveDirectory: ERB -> erb ({erbDir})");
        Check(Directory.Exists(PathResolver.ResolveDirectory(csvDirGuess)),
            "ResolveDirectory: csv -> Csv");

        // --- GetFiles: 이번 버그의 핵심 -------------------------------------
        // Directory.GetFiles 는 Linux/Android 에서 패턴의 대소문자까지 구분하므로
        // "*.ERB" 로는 system.erb / shop.Erb 를 찾지 못했다.
        var top = PathResolver.GetFiles(erbDir, "*.ERB", SearchOption.TopDirectoryOnly);
        Check(top.Length == 3,
            $"GetFiles(\"*.ERB\"): .erb/.Erb/.ERB 를 모두 찾는다 (기대 3, 실제 {top.Length}: {Names(top)})");
        Check(!Contains(top, "notes.erbak"),
            "GetFiles: 확장자가 더 긴 파일(.erbak)은 걸리지 않는다");

        var all = PathResolver.GetFiles(erbDir, "*.ERB", SearchOption.AllDirectories);
        Check(all.Length == 4,
            $"GetFiles(AllDirectories): 하위 폴더까지 훑는다 (기대 4, 실제 {all.Length}: {Names(all)})");

        var chara = PathResolver.GetFiles(PathResolver.ResolveDirectory(csvDirGuess),
            "CHARA*.CSV", SearchOption.TopDirectoryOnly);
        Check(chara.Length == 1,
            $"GetFiles(\"CHARA*.CSV\"): chara0.csv 를 찾는다 (실제 {chara.Length}: {Names(chara)})");

        var none = PathResolver.GetFiles(Path.Combine(root, "no_such_dir"), "*.ERB",
            SearchOption.TopDirectoryOnly);
        Check(none.Length == 0, "GetFiles: 없는 폴더는 예외 없이 빈 배열");

        // --- 엔진 경로 (Config.GetFiles) -------------------------------------
        // ErbLoader / HeaderFileLoader / ConstantData 가 실제로 쓰는 진입점.
        // 여기가 막혀 있었기 때문에 게임이 통째로 로드되지 않았다.
        var viaConfig = Config.GetFiles(erbDir, "*.ERB");
        Check(viaConfig.Count == 3,
            $"Config.GetFiles(\"*.ERB\"): 엔진 진입점도 대소문자를 무시한다 (기대 3, 실제 {viaConfig.Count})");

        // --- 캐시 무효화 ------------------------------------------------------
        // 앱을 켠 채로 게임을 복사해 넣는 경우가 흔하다. 캐시가 남으면
        // 새로 넣은 파일이 영원히 보이지 않는다.
        var added = Path.Combine(erbDir, "added.erb");
        File.WriteAllText(added, "@ADDED\n");
        PathResolver.ClearCache();
        var afterAdd = PathResolver.GetFiles(erbDir, "*.ERB", SearchOption.TopDirectoryOnly);
        Check(afterAdd.Length == 4,
            $"ClearCache 후 새 파일이 보인다 (기대 4, 실제 {afterAdd.Length})");
        Check(File.Exists(PathResolver.ResolveFile(Path.Combine(erbDir, "ADDED.ERB"))),
            "ClearCache 후 새 파일을 대소문자 무시로 찾는다");
    }

    // ----------------------------------------------------------------------
    // GameBase.csv
    //
    // era 포맷 문서(eramaker CSV 규격)의 지시어가 실제로 적용되는지 확인한다.
    // 지시어 이름에 공백이 붙으면 switch 가 어디에도 걸리지 않아 "코드"가
    // 조용히 0이 되는데, 코드 0인 세이브는 아무 게임에서나 열리므로 가장 위험하다.
    // ----------------------------------------------------------------------
    static void RunGameBaseChecks(string root)
    {
        var path = Path.Combine(root, "gamebase_test.csv");
        File.WriteAllText(path, string.Join("\n", new[]
        {
            ";주석 줄은 무시된다",
            "",
            "コード ,12345",              // 지시어 뒤 공백 — 트림 없이는 무시된다
            "バージョン,1200",
            "アイテムなし,1",
            "最初からいるキャラ,3",
            "タイトル, 테스트 게임 ",      // 값의 공백은 트림된다
            "動作に必要なEmueraのバージョン,1.800.0.0",
            "作者,작성자",                // 위 버전 줄에서 중단되면 이 줄을 잃는다
        }) + "\n");

        var gb = new GameBase();
        bool ok = gb.LoadGameBaseCsv(path);

        Check(ok, "GameBase: 요구 버전을 만족하면 로드를 계속한다");
        Check(gb.ScriptUniqueCode == 12345,
            $"GameBase: 지시어 뒤 공백이 있어도 「コード」가 적용된다 (실제 {gb.ScriptUniqueCode})");
        Check(gb.ScriptVersion == 1200,
            $"GameBase: 「バージョン」 (실제 {gb.ScriptVersion})");
        Check(gb.DefaultNoItem == 1,
            $"GameBase: 「アイテムなし」 -> NOITEM 초기값 (실제 {gb.DefaultNoItem})");
        Check(gb.DefaultCharacter == 3,
            $"GameBase: 「最初からいるキャラ」 (실제 {gb.DefaultCharacter})");
        Check(gb.ScriptTitle == "테스트 게임",
            $"GameBase: 「タイトル」의 앞뒤 공백은 트림된다 (실제 '{gb.ScriptTitle}')");
        Check(gb.ScriptAutherName == "작성자",
            $"GameBase: 버전 지정 줄 뒤의 항목도 살아남는다 (실제 '{gb.ScriptAutherName}')");

        // 엔진 버전 문자열이 System.Version 으로 파싱 가능해야 한다.
        // 비어 있으면 new Version("") 이 예외를 던져 GAMEBASE 파싱이 중단됐다.
        var engineVer = uEmuera.Window.MainWindow.uEmueraVer;
        bool verOk = System.Text.RegularExpressions.Regex.IsMatch(
            engineVer ?? "", @"^\d+\.\d+\.\d+\.\d+$");
        Check(verOk, $"엔진 버전 문자열이 '수.수.수.수' 형식이다 (실제 '{engineVer}')");

        // 요구 버전이 엔진보다 높으면 거부해야 한다(게이트가 살아 있는지).
        var path2 = Path.Combine(root, "gamebase_future.csv");
        File.WriteAllText(path2, "動作に必要なEmueraのバージョン,99.999.9.9\n");
        var gb2 = new GameBase();
        Check(!gb2.LoadGameBaseCsv(path2),
            "GameBase: 엔진보다 높은 버전을 요구하면 거부한다");
    }

    // ----------------------------------------------------------------------
    // 배열 상한
    //
    // era 포맷 문서에 명시된 상한과 엔진 기본값이 일치해야 한다.
    // 어긋나면 게임이 FLAG:9999 등에 쓸 때 "배열 범위를 벗어났습니다" 가 된다.
    // ----------------------------------------------------------------------
    static void RunArraySizeChecks()
    {
        var c = new ConstantData();
        int Var(VariableCode code)
            => c.VariableIntArrayLength[(int)(VariableCode.__LOWERCASE__ & code)];
        int Chara(VariableCode code)
            => c.CharacterIntArrayLength[(int)(VariableCode.__LOWERCASE__ & code)];
        int VarStr(VariableCode code)
            => c.VariableStrArrayLength[(int)(VariableCode.__LOWERCASE__ & code)];

        Check(Var(VariableCode.FLAG) == 10000, $"FLAG 0-9999 (실제 {Var(VariableCode.FLAG)})");
        Check(Var(VariableCode.TFLAG) == 1000, $"TFLAG 0-999 (실제 {Var(VariableCode.TFLAG)})");
        Check(Chara(VariableCode.TALENT) == 1000, $"TALENT 0-999 (실제 {Chara(VariableCode.TALENT)})");
        Check(Chara(VariableCode.CFLAG) == 1000, $"CFLAG 0-999 (실제 {Chara(VariableCode.CFLAG)})");
        Check(Chara(VariableCode.JUEL) == 200, $"JUEL 0-199 (실제 {Chara(VariableCode.JUEL)})");
        Check(Chara(VariableCode.ABL) == 100, $"ABL 0-99 (실제 {Chara(VariableCode.ABL)})");
        Check(Chara(VariableCode.EXP) == 100, $"EXP 0-99 (실제 {Chara(VariableCode.EXP)})");
        Check(Chara(VariableCode.MARK) == 100, $"MARK 0-99 (실제 {Chara(VariableCode.MARK)})");
        Check(Chara(VariableCode.RELATION) == 100, $"RELATION 0-99 (실제 {Chara(VariableCode.RELATION)})");
        Check(VarStr(VariableCode.STR) == 20000, $"STR 0-19999 (실제 {VarStr(VariableCode.STR)})");
        Check(VarStr(VariableCode.SAVESTR) == 100, $"SAVESTR 0-99 (실제 {VarStr(VariableCode.SAVESTR)})");
    }

    // ----------------------------------------------------------------------
    // Emuera EM+EE 확장
    //
    // 규격: https://gitlab.com/EvilMask/emuera.em.doc
    // 반환값 규약이 문서에 명시돼 있어(맵 없으면 -1, MAP_GET 은 빈 문자열 등)
    // 그대로 검증한다. 여기가 틀리면 게임 쪽 분기가 조용히 어긋난다.
    // ----------------------------------------------------------------------
    static void RunEmExtensionChecks()
    {
        EmMapStore.ClearAll();

        // 생성 / 존재 / 중복
        Check(EmMapStore.Exists("m") == 0, "MAP_EXIST: 없는 맵은 0");
        Check(EmMapStore.Create("m") == 1, "MAP_CREATE: 새 맵은 1");
        Check(EmMapStore.Create("m") == 0, "MAP_CREATE: 이미 있으면 0");
        Check(EmMapStore.Exists("m") == 1, "MAP_EXIST: 있으면 1");

        // 맵이 없을 때의 반환값 (-1 규약)
        Check(EmMapStore.Size("nope") == -1, "MAP_SIZE: 맵이 없으면 -1");
        Check(EmMapStore.Has("nope", "k") == -1, "MAP_HAS: 맵이 없으면 -1");
        Check(EmMapStore.Set("nope", "k", "v") == -1, "MAP_SET: 맵이 없으면 -1");
        Check(EmMapStore.Remove("nope", "k") == -1, "MAP_REMOVE: 맵이 없으면 -1");
        Check(EmMapStore.Clear("nope") == -1, "MAP_CLEAR: 맵이 없으면 -1");
        Check(EmMapStore.Get("nope", "k") == "",
            "MAP_GET: 맵이 없으면 빈 문자열(예외를 던지지 않는다)");

        // 정상 동작
        Check(EmMapStore.Size("m") == 0, "MAP_SIZE: 빈 맵은 0");
        Check(EmMapStore.Set("m", "Id", "user") == 1, "MAP_SET: 추가하면 1");
        Check(EmMapStore.Set("m", "Pw", "1234") == 1, "MAP_SET: 두 번째 추가");
        Check(EmMapStore.Size("m") == 2, $"MAP_SIZE: 2 (실제 {EmMapStore.Size("m")})");
        Check(EmMapStore.Get("m", "Id") == "user", "MAP_GET: 값을 읽는다");
        Check(EmMapStore.Has("m", "Id") == 1, "MAP_HAS: 있으면 1");
        Check(EmMapStore.Has("m", "None") == 0, "MAP_HAS: 없는 키는 0");
        Check(EmMapStore.Get("m", "None") == "", "MAP_GET: 없는 키는 빈 문자열");

        // 덮어쓰기도 1
        Check(EmMapStore.Set("m", "Id", "other") == 1, "MAP_SET: 덮어써도 1");
        Check(EmMapStore.Get("m", "Id") == "other", "MAP_SET: 값이 바뀐다");
        Check(EmMapStore.Size("m") == 2, "MAP_SET: 덮어쓰기는 개수를 늘리지 않는다");

        // 키는 대소문자를 구분한다
        Check(EmMapStore.Has("m", "id") == 0, "MAP: 키는 대소문자를 구분한다");

        // 삭제 / 비우기 / 해제
        Check(EmMapStore.Remove("m", "Pw") == 1, "MAP_REMOVE: 1");
        Check(EmMapStore.Size("m") == 1, "MAP_REMOVE: 개수가 줄어든다");
        Check(EmMapStore.Clear("m") == 1, "MAP_CLEAR: 1");
        Check(EmMapStore.Size("m") == 0, "MAP_CLEAR: 비워진다");
        Check(EmMapStore.Release("m") == 1, "MAP_RELEASE: 항상 1");
        Check(EmMapStore.Exists("m") == 0, "MAP_RELEASE: 사라진다");

        // 키 목록
        EmMapStore.Create("k");
        EmMapStore.Set("k", "a", "1");
        EmMapStore.Set("k", "b", "2");
        var keys = EmMapStore.Keys("k");
        Check(keys != null && keys.Count == 2, "MAP_GETKEYS: 키 2개");
        Check(EmMapStore.Keys("nope") == null, "MAP_GETKEYS: 맵이 없으면 null");
        EmMapStore.ClearAll();
        Check(EmMapStore.Exists("k") == 0, "ClearAll: RESETDATA 에서 전부 지워진다");

        // --- DataTable (DT_*) ------------------------------------------------
        // EM 문서의 DT_SELECT 예제를 그대로 재현한다.
        EmDataTableStore.ClearAll();
        Check(EmDataTableStore.Exists("db") == 0, "DT_EXIST: 없는 테이블은 0");
        Check(EmDataTableStore.Create("db") == 1, "DT_CREATE: 새 테이블은 1");
        Check(EmDataTableStore.Create("db") == 0, "DT_CREATE: 이미 있으면 0");
        // id 열은 생성 직후 자동으로 붙는다
        Check(EmDataTableStore.ColumnLength("db") == 1,
            $"DT_CREATE: id 열이 자동 추가된다 (실제 {EmDataTableStore.ColumnLength("db")})");
        Check(EmDataTableStore.ColumnExist("db", "id") != 0, "DT_COLUMN_EXIST: id");
        Check(EmDataTableStore.ColumnLength("nope") == -1, "DT_COLUMN_LENGTH: 없으면 -1");

        Check(EmDataTableStore.ColumnAdd("db", "name", null, 0, 1) == 1, "DT_COLUMN_ADD: name");
        Check(EmDataTableStore.ColumnAdd("db", "height", "int16", 0, 1) == 1,
            "DT_COLUMN_ADD: int16");
        Check(EmDataTableStore.ColumnAdd("db", "age", "int16", 0, 1) == 1, "DT_COLUMN_ADD: age");
        Check(EmDataTableStore.ColumnAdd("db", "name", null, 0, 1) == 0,
            "DT_COLUMN_ADD: 중복이면 0");
        // 타입 번호 규약: 2 = int16, 5 = string
        Check(EmDataTableStore.ColumnExist("db", "age") == 2,
            $"DT_COLUMN_EXIST: int16 은 2 (실제 {EmDataTableStore.ColumnExist("db", "age")})");
        Check(EmDataTableStore.ColumnExist("db", "name") == 5,
            $"DT_COLUMN_EXIST: string 은 5 (실제 {EmDataTableStore.ColumnExist("db", "name")})");
        Check(EmDataTableStore.ColumnExist("db", "none") == 0, "DT_COLUMN_EXIST: 없으면 0");
        Check(EmDataTableStore.ColumnRemove("db", "id") == 0, "DT_COLUMN_REMOVE: id 는 못 지운다");

        static long AddRow(string n, string v, long age, long h)
            => EmDataTableStore.RowAdd("db", new List<KeyValuePair<string, string>>
            {
                new("name", v), new("age", age.ToString()), new("height", h.ToString()),
            });
        long id1 = AddRow("db", "Name1", 11, 132);
        AddRow("db", "Name2", 21, 164);
        AddRow("db", "Name3", 18, 159);
        AddRow("db", "Name4", 33, 180);
        AddRow("db", "Name5", 18, 172);
        Check(id1 == 0, $"DT_ROW_ADD: 첫 행의 id 는 0 (실제 {id1})");
        Check(EmDataTableStore.RowLength("db") == 5,
            $"DT_ROW_LENGTH: 5 (실제 {EmDataTableStore.RowLength("db")})");

        // 셀 읽기. asId=1 은 id 로, 그 외는 0 기준 순번
        Check(EmDataTableStore.CellGetStr("db", 0, "name", 1) == "Name1",
            "DT_CELL_GETS: asId=1 은 id 로 찾는다");
        Check(EmDataTableStore.CellGetInt("db", 0, "age", 1) == 11, "DT_CELL_GET: 정수 열");
        Check(EmDataTableStore.CellGetStr("db", 2, "name", 0) == "Name3",
            "DT_CELL_GETS: asId=0 은 순번으로 찾는다");
        Check(EmDataTableStore.CellGetInt("db", 99, "age", 1) == 0,
            "DT_CELL_GET: 실패하면 0");
        Check(EmDataTableStore.CellGetStr("db", 99, "name", 1) == "",
            "DT_CELL_GETS: 실패하면 빈 문자열");
        Check(EmDataTableStore.CellIsNull("db", 0, "none", 1) == -2,
            "DT_CELL_ISNULL: 열이 없으면 -2");
        Check(EmDataTableStore.CellSet("db", 0, "none", "x", 1) == -3,
            "DT_CELL_SET: 열이 없으면 -3");
        Check(EmDataTableStore.CellSet("db", 0, "age", "12", 1) == 1, "DT_CELL_SET: 1");
        Check(EmDataTableStore.CellGetInt("db", 0, "age", 1) == 12, "DT_CELL_SET: 값이 바뀐다");

        // DT_SELECT — System.Data.DataTable.Select 문법.
        // 여기가 통과하면 BCL 위임이 실제로 동작한다는 뜻이다.
        var sel = EmDataTableStore.Select("db", "age >= 18", "age ASC, height DESC");
        Check(sel != null && sel.Count == 4,
            $"DT_SELECT: age>=18 이 4건 (실제 {(sel == null ? "null" : sel.Count.ToString())})");
        if (sel != null && sel.Count == 4)
        {
            // 문서 예제의 기대 순서: Name5(18,172), Name3(18,159), Name2(21), Name4(33)
            var names = new List<string>();
            foreach (var id in sel)
                names.Add(EmDataTableStore.CellGetStr("db", id, "name", 1));
            var joined = string.Join(",", names);
            Check(joined == "Name5,Name3,Name2,Name4",
                $"DT_SELECT: 정렬까지 문서 예제와 일치 (실제 {joined})");
        }
        Check(EmDataTableStore.Select("db", "이건 잘못된 식 ((", null) == null,
            "DT_SELECT: 잘못된 필터식은 null (엔진을 죽이지 않는다)");
        Check(EmDataTableStore.Select("nope", null, null) == null,
            "DT_SELECT: 테이블이 없으면 null");

        Check(EmDataTableStore.RowRemove("db", 0) == 1, "DT_ROW_REMOVE: 1");
        Check(EmDataTableStore.RowLength("db") == 4, "DT_ROW_REMOVE: 개수가 줄어든다");
        Check(EmDataTableStore.RowRemove("db", 999) == 0, "DT_ROW_REMOVE: 없는 id 는 0");
        Check(EmDataTableStore.Clear("db") == 1, "DT_CLEAR: 1");
        Check(EmDataTableStore.RowLength("db") == 0, "DT_CLEAR: 비워진다");
        Check(EmDataTableStore.Release("db") == 1, "DT_RELEASE: 항상 1");
        Check(EmDataTableStore.Exists("db") == 0, "DT_RELEASE: 사라진다");
        EmDataTableStore.ClearAll();

        // --- XML (XML_*) -----------------------------------------------------
        EmXmlStore.ClearAll();
        const string xml = "<root><item id=\"1\"><n>alpha</n></item>"
                         + "<item id=\"2\"><n>beta</n></item></root>";
        Check(EmXmlStore.Exists("x") == 0, "XML_EXIST: 없으면 0");
        Check(EmXmlStore.Create("x", xml) == 1, "XML_DOCUMENT: 1");
        Check(EmXmlStore.Create("x", xml) == 0, "XML_DOCUMENT: 이미 있으면 0");
        Check(EmXmlStore.Exists("x") == 1, "XML_EXIST: 있으면 1");
        Check(EmXmlStore.Create("bad", "<unclosed>") == 0,
            "XML_DOCUMENT: 파싱 실패는 0 (예외를 던지지 않는다)");

        // outputType: 1=InnerText, 3=OuterXml, 4=Name
        var t1 = EmXmlStore.Get("x", "/root/item/n", 1);
        Check(t1 != null && t1.Count == 2 && t1[0] == "alpha" && t1[1] == "beta",
            $"XML_GET: InnerText 2건 (실제 {(t1 == null ? "null" : string.Join(",", t1))})");
        var t4 = EmXmlStore.Get("x", "/root/item", 4);
        Check(t4 != null && t4.Count == 2 && t4[0] == "item", "XML_GET: outputType 4 = Name");
        // 속성값은 Value 로 읽힌다(outputType 미지정)
        var attr = EmXmlStore.Get("x", "/root/item/@id", 0);
        Check(attr != null && attr.Count == 2 && attr[0] == "1",
            $"XML_GET: 속성은 Value (실제 {(attr == null ? "null" : string.Join(",", attr))})");
        Check(EmXmlStore.Get("nope", "/root", 1) == null, "XML_GET: 문서가 없으면 null");
        Check(EmXmlStore.Get("x", "///[[bad", 1) == null,
            "XML_GET: 잘못된 XPath 는 null (엔진을 죽이지 않는다)");
        var noHit = EmXmlStore.Get("x", "/root/nothing", 1);
        Check(noHit != null && noHit.Count == 0, "XML_GET: 안 맞으면 0건");

        // 저장하지 않은 XML 문자열에서 바로 읽기 (XML_GET 형태 1)
        var direct = EmXmlStore.GetFromContent(xml, "/root/item/n", 1);
        Check(direct != null && direct.Count == 2,
            "XML_GET: 문자열 인수면 그 내용을 직접 파싱한다");

        // 편집
        Check(EmXmlStore.Set("x", "/root/item/n", "gamma", 1, 1) == 2,
            "XML_SET: doSetAll=1 이면 전부");
        var afterSet = EmXmlStore.Get("x", "/root/item/n", 1);
        Check(afterSet != null && afterSet[0] == "gamma", "XML_SET: 값이 바뀐다");
        Check(EmXmlStore.Set("nope", "/root", "v", 1, 1) == -1, "XML_SET: 문서가 없으면 -1");

        Check(EmXmlStore.AddNode("x", "/root", "<extra/>", 0, 0) == 1, "XML_ADDNODE: 1건");
        Check(EmXmlStore.Get("x", "/root/extra", 4)?.Count == 1, "XML_ADDNODE: 실제로 붙는다");
        Check(EmXmlStore.AddAttribute("x", "/root/extra", "k", "v", 1) == 1,
            "XML_ADDATTRIBUTE: 1건");
        Check(EmXmlStore.Get("x", "/root/extra/@k", 0)?[0] == "v",
            "XML_ADDATTRIBUTE: 값이 들어간다");
        Check(EmXmlStore.RemoveAttribute("x", "/root/extra", "k", 1) == 1,
            "XML_REMOVEATTRIBUTE: 1건");
        Check(EmXmlStore.Get("x", "/root/extra/@k", 0)?.Count == 0,
            "XML_REMOVEATTRIBUTE: 사라진다");
        Check(EmXmlStore.RemoveNode("x", "/root/item", 1) == 2,
            "XML_REMOVENODE: doSetAll=1 이면 전부");
        Check(EmXmlStore.Get("x", "/root/item", 4)?.Count == 0, "XML_REMOVENODE: 사라진다");

        Check(EmXmlStore.ToStr("x").Contains("extra"), "XML_TOSTR: 문서 전체를 돌려준다");
        Check(EmXmlStore.ToStr("nope") == "", "XML_TOSTR: 없으면 빈 문자열");
        Check(EmXmlStore.Release("x") == 1, "XML_RELEASE: 항상 1");
        Check(EmXmlStore.Exists("x") == 0, "XML_RELEASE: 사라진다");
        EmXmlStore.ClearAll();

        // 명령/표현식 양쪽 등록 확인.
        // FunctionIdentifier 가 methodList 의 항목을 METHOD_Instruction 으로
        // 감싸 명령으로도 등록한다. 등록이 빠지면 「해석할 수 없는 식별자」가 된다.
        var methods = MinorShift.Emuera.GameData.Function
            .FunctionMethodCreator.GetMethodList();
        foreach (var name in new[]
        {
            "MAP_CREATE", "MAP_EXIST", "MAP_RELEASE", "MAP_CLEAR",
            "MAP_GET", "MAP_HAS", "MAP_SET", "MAP_REMOVE", "MAP_SIZE",
            "EXISTFUNCTION", "HTML_STRINGLEN",
            "CBRT", "LOG", "LOG10", "EXPONENT",
            "DT_CREATE", "DT_EXIST", "DT_RELEASE", "DT_CLEAR", "DT_NOCASE",
            "DT_COLUMN_ADD", "DT_COLUMN_EXIST", "DT_COLUMN_REMOVE",
            "DT_COLUMN_LENGTH", "DT_COLUMN_OPTIONS",
            "DT_ROW_ADD", "DT_ROW_SET", "DT_ROW_REMOVE", "DT_ROW_LENGTH",
            "DT_CELL_GET", "DT_CELL_GETS", "DT_CELL_ISNULL", "DT_CELL_SET",
            "DT_SELECT",
            "XML_DOCUMENT", "XML_EXIST", "XML_RELEASE", "XML_TOSTR",
            "XML_GET", "XML_GET_BYNAME", "XML_SET", "XML_SET_BYNAME",
            "XML_ADDNODE", "XML_ADDNODE_BYNAME", "XML_REMOVENODE",
            "XML_ADDATTRIBUTE", "XML_REMOVEATTRIBUTE",
            "PLAYBGM", "PLAYSOUND", "STOPBGM", "STOPSOUND",
            "SETBGMVOLUME", "SETSOUNDVOLUME", "EXISTSOUND",
        })
        {
            Check(methods.ContainsKey(name), $"확장 함수 등록: {name}");
        }
    }

    static bool Contains(IEnumerable<string> paths, string filename)
    {
        foreach (var p in paths)
            if (string.Equals(Path.GetFileName(p), filename, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static string Names(IEnumerable<string> paths)
    {
        var names = new List<string>();
        foreach (var p in paths)
            names.Add(Path.GetFileName(p));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", names);
    }
}
