using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 대소문자를 무시해 실제 파일 경로를 찾아준다.
///
/// Emuera 엔진은 파일명을 대문자로 하드코딩한다("GAMEBASE.CSV", "ABL.CSV" 등).
/// Windows는 파일 시스템이 대소문자를 구분하지 않아 실제 파일이
/// gamebase.csv 여도 열렸지만, Android/Linux는 구분하므로 열리지 않는다.
/// 그래서 PC에서는 되고 실기에서만 게임이 로드되지 않는 증상이 나온다.
///
/// 정확한 경로가 존재하면 그대로 반환하므로(빠른 경로) 올바른 대소문자로
/// 배포된 게임에는 비용이 없다. 실패했을 때만 디렉터리를 훑고, 그 결과를
/// 캐시한다(ERB 로딩은 파일을 수백 개 열기 때문에 캐시가 없으면 느려진다).
/// </summary>
internal static class PathResolver
{
    // 디렉터리 경로 → (소문자 파일명 → 실제 파일명)
    static readonly Dictionary<string, Dictionary<string, string>> dirCache =
        new(StringComparer.OrdinalIgnoreCase);
    static readonly object sync = new object();

    /// <summary>
    /// 파일 경로를 해석한다. 찾지 못하면 원본을 그대로 돌려주므로
    /// 호출부의 기존 오류 처리가 그대로 동작한다.
    /// </summary>
    internal static string ResolveFile(string path)
        => Resolve(path, wantDirectory: false);

    /// <summary>디렉터리 경로를 해석한다.</summary>
    internal static string ResolveDirectory(string path)
        => Resolve(path, wantDirectory: true);

    static string Resolve(string path, bool wantDirectory)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // 빠른 경로: 그대로 존재하면 훑지 않는다
        if (wantDirectory ? Directory.Exists(path) : File.Exists(path))
            return path;

        string dir;
        string name;
        try
        {
            // 끝에 구분자가 붙은 디렉터리 경로도 처리한다
            var trimmed = path.TrimEnd('/', '\\');
            dir = Path.GetDirectoryName(trimmed) ?? "";
            name = Path.GetFileName(trimmed);
        }
        catch
        {
            return path;
        }

        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
            return path;

        // 부모 디렉터리 자체도 대소문자가 틀릴 수 있으므로 재귀적으로 해석한다
        var realDir = Directory.Exists(dir) ? dir : Resolve(dir, wantDirectory: true);
        if (!Directory.Exists(realDir))
            return path;

        var map = GetEntries(realDir);
        if (map != null && map.TryGetValue(name, out var actual))
            return Path.Combine(realDir, actual);

        // 부모만 해석된 경우라도 그 결과로 조합해 돌려준다
        return realDir == dir ? path : Path.Combine(realDir, name);
    }

    static Dictionary<string, string>? GetEntries(string dir)
    {
        lock (sync)
        {
            if (dirCache.TryGetValue(dir, out var cached))
                return cached;
        }

        Dictionary<string, string> map;
        try
        {
            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                var fn = Path.GetFileName(entry);
                if (!string.IsNullOrEmpty(fn))
                    map[fn] = fn;   // 대소문자 무시 비교로 실제 이름을 찾는다
            }
        }
        catch (Exception e)
        {
            uEmuera.Logger.Warn($"PathResolver: '{dir}' 의 파일 목록을 읽을 수 없습니다: {e.Message}");
            return null;
        }

        lock (sync)
            dirCache[dir] = map;
        return map;
    }

    /// <summary>
    /// 게임을 바꿀 때 호출한다. 캐시를 남겨두면 이전 게임의 목록을 보게 된다.
    /// </summary>
    internal static void ClearCache()
    {
        lock (sync)
            dirCache.Clear();
    }
}
