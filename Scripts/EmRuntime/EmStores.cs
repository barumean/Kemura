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

    /// <summary>
    /// MAP_TOXML: 규격이 정한 형태로 직렬화한다.
    ///
    ///   &lt;map&gt;&lt;p&gt;&lt;k&gt;키&lt;/k&gt;&lt;v&gt;값&lt;/v&gt;&lt;/p&gt;...&lt;/map&gt;
    ///
    /// 맵이 없으면 빈 문자열. 규격이 "예외를 던지지 않는다"고 명시한다.
    /// XmlWriter 를 쓰는 이유는 키·값에 &lt; &amp; 같은 문자가 들어와도
    /// 깨지지 않게 하기 위함이다. 문자열을 이어 붙이면 MAP_FROMXML 로
    /// 되돌릴 수 없는 XML 이 만들어진다.
    /// </summary>
    internal static string ToXml(string name)
    {
        if (!TryGet(name, out var m))
            return "";
        var sb = new System.Text.StringBuilder();
        var settings = new System.Xml.XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
        };
        using (var w = System.Xml.XmlWriter.Create(sb, settings))
        {
            w.WriteStartElement("map");
            foreach (var kv in m)
            {
                w.WriteStartElement("p");
                w.WriteElementString("k", kv.Key);
                w.WriteElementString("v", kv.Value ?? "");
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        return sb.ToString();
    }

    /// <summary>
    /// MAP_FROMXML: XML 의 키·값을 맵에 덮어쓴다. 성공 1, 실패 0.
    /// 맵이 없으면 만들지 않고 0 (규격에 만든다는 언급이 없다).
    /// </summary>
    internal static long FromXml(string name, string xml)
    {
        if (!TryGet(name, out var m))
            return 0;
        if (string.IsNullOrWhiteSpace(xml))
            return 0;
        try
        {
            var doc = new System.Xml.XmlDocument
            {
                // 외부 엔티티 참조를 막는다(XXE). 게임 파일에서 오는 XML 이다.
                XmlResolver = null,
            };
            doc.LoadXml(xml);
            var pairs = doc.SelectNodes("/map/p");
            if (pairs == null)
                return 0;
            foreach (System.Xml.XmlNode p in pairs)
            {
                var k = p.SelectSingleNode("k")?.InnerText;
                if (k == null)
                    continue;
                m[k] = p.SelectSingleNode("v")?.InnerText ?? "";
            }
            return 1;
        }
        catch (System.Xml.XmlException)
        {
            return 0;
        }
    }

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
