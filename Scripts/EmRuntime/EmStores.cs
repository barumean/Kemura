using System;
using System.Collections.Generic;

/// <summary>
/// Emuera EM+EE 확장이 쓰는 이름 기반 런타임 저장소.
///
/// 규격: https://gitlab.com/EvilMask/emuera.em.doc
/// EM 은 자체 의미를 .NET BCL 타입으로 직접 규정한다. MAP 은
/// <c>Dictionary&lt;string,string&gt;</c> 라고 문서가 명시하므로 그대로 쓴다.
///
/// 수명: 「타이틀로 돌아가기」와 RESETDATA 에서 모두 지워진다(EM 규격).
/// <see cref="ClearAll"/> 가 그 지점에서 호출된다.
///
/// 스레드: 엔진 스레드에서만 접근한다. Godot 메인 스레드는 이 저장소를
/// 건드리지 않으므로 락을 두지 않는다.
/// </summary>
internal static class EmMapStore
{
    // EM 의 맵 이름은 대소문자를 구분한다(문서에 무시한다는 언급이 없음).
    static readonly Dictionary<string, Dictionary<string, string>> maps =
        new(StringComparer.Ordinal);

    /// <summary>이미 있으면 0, 새로 만들면 1.</summary>
    internal static long Create(string name)
    {
        if (name == null) return 0;
        if (maps.ContainsKey(name)) return 0;
        maps[name] = new Dictionary<string, string>(StringComparer.Ordinal);
        return 1;
    }

    internal static long Exists(string name)
        => name != null && maps.ContainsKey(name) ? 1 : 0;

    /// <summary>EM 규격상 항상 1 을 반환한다.</summary>
    internal static long Release(string name)
    {
        if (name != null) maps.Remove(name);
        return 1;
    }

    /// <summary>맵이 없으면 -1.</summary>
    internal static long Clear(string name)
    {
        if (!TryGet(name, out var m)) return -1;
        m.Clear();
        return 1;
    }

    /// <summary>
    /// 키나 맵이 없으면 빈 문자열. 예외를 던지지 않는다(EM 규격).
    /// </summary>
    internal static string Get(string name, string key)
    {
        if (!TryGet(name, out var m) || key == null) return "";
        return m.TryGetValue(key, out var v) ? v : "";
    }

    /// <summary>있으면 1, 없으면 0, 맵 자체가 없으면 -1.</summary>
    internal static long Has(string name, string key)
    {
        if (!TryGet(name, out var m)) return -1;
        return key != null && m.ContainsKey(key) ? 1 : 0;
    }

    /// <summary>덮어쓰거나 추가하고 1. 맵이 없으면 -1.</summary>
    internal static long Set(string name, string key, string value)
    {
        if (!TryGet(name, out var m)) return -1;
        if (key == null) return -1;
        m[key] = value ?? "";
        return 1;
    }

    /// <summary>지우고 1. 맵이 없으면 -1.</summary>
    internal static long Remove(string name, string key)
    {
        if (!TryGet(name, out var m)) return -1;
        if (key != null) m.Remove(key);
        return 1;
    }

    /// <summary>쌍의 개수. 맵이 없으면 -1.</summary>
    internal static long Size(string name)
        => TryGet(name, out var m) ? m.Count : -1;

    /// <summary>키 목록. 맵이 없으면 null.</summary>
    internal static List<string>? Keys(string name)
        => TryGet(name, out var m) ? new List<string>(m.Keys) : null;

    static bool TryGet(string? name, out Dictionary<string, string> map)
    {
        if (name != null && maps.TryGetValue(name, out var m))
        {
            map = m;
            return true;
        }
        map = null!;
        return false;
    }

    /// <summary>타이틀 복귀 / RESETDATA 시 호출.</summary>
    internal static void ClearAll() => maps.Clear();
}
