using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using MinorShift.Emuera;

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
