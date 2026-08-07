using System;
using System.Collections.Generic;
using System.Xml;

/// <summary>
/// Emuera EM 확장의 <c>XML_*</c> 저장소.
///
/// 규격: https://gitlab.com/EvilMask/emuera.em.doc
/// EM 문서가 <c>System.Xml.XmlDocument</c> 로 규정하므로 BCL 을 그대로 감싼다.
/// 이름(<c>xmlId</c>)으로 문서를 보관한다. 정수 id 는 호출부에서 TOSTR 되어
/// 문자열 키로 들어온다.
///
/// 수명: EM 규격대로 RESETDATA 와 타이틀 복귀에서 삭제된다.
/// 스레드: 엔진 스레드에서만 접근한다.
/// </summary>
internal static class EmXmlStore
{
    static readonly Dictionary<string, XmlDocument> docs = new(StringComparer.Ordinal);

    /// <summary>EM 의 outputType. 지정하지 않으면 노드의 Value.</summary>
    internal static string NodeText(XmlNode node, long outputType) => outputType switch
    {
        1 => node.InnerText ?? "",
        2 => node.InnerXml ?? "",
        3 => node.OuterXml ?? "",
        4 => node.Name ?? "",
        _ => node.Value ?? "",
    };

    // ------------------------------------------------------------------
    // 관리
    // ------------------------------------------------------------------

    /// <summary>이미 있으면 0, 파싱 성공하면 1, 파싱 실패하면 0.</summary>
    internal static long Create(string name, string content)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        if (docs.ContainsKey(name)) return 0;
        var d = Parse(content);
        if (d == null) return 0;
        docs[name] = d;
        return 1;
    }

    internal static long Exists(string name)
        => name != null && docs.ContainsKey(name) ? 1 : 0;

    /// <summary>EM 규격상 항상 1.</summary>
    internal static long Release(string name)
    {
        if (name != null) docs.Remove(name);
        return 1;
    }

    /// <summary>문서 전체를 문자열로. 없으면 빈 문자열.</summary>
    internal static string ToStr(string name)
        => docs.TryGetValue(name ?? "", out var d) ? d.OuterXml ?? "" : "";

    // ------------------------------------------------------------------
    // 조회
    // ------------------------------------------------------------------

    /// <summary>
    /// xpath 로 노드를 골라 outputType 에 따른 문자열 목록을 돌려준다.
    /// 문서가 없으면 null(호출부가 -1 을 반환한다).
    /// </summary>
    internal static List<string>? Get(string name, string xpath, long outputType)
    {
        if (!docs.TryGetValue(name ?? "", out var d)) return null;
        return Select(d, xpath, outputType);
    }

    /// <summary>저장하지 않은 XML 문자열에서 바로 고른다.</summary>
    internal static List<string>? GetFromContent(string content, string xpath, long outputType)
    {
        var d = Parse(content);
        if (d == null) return null;
        return Select(d, xpath, outputType);
    }

    static List<string>? Select(XmlDocument d, string xpath, long outputType)
    {
        if (string.IsNullOrEmpty(xpath)) return new List<string>();
        XmlNodeList? nodes;
        try
        {
            nodes = d.SelectNodes(xpath);
        }
        catch (Exception e)
        {
            // 잘못된 XPath 는 게임 쪽 실수다. 엔진을 죽이지 않고 알린다.
            uEmuera.Logger.Warn($"XML_GET('{xpath}'): {e.Message}");
            return null;
        }
        var list = new List<string>();
        if (nodes == null) return list;
        foreach (XmlNode n in nodes)
            list.Add(NodeText(n, outputType));
        return list;
    }

    // ------------------------------------------------------------------
    // 편집
    // ------------------------------------------------------------------

    /// <summary>
    /// xpath 로 고른 노드에 값을 넣는다. 바꾼 노드 수를 돌려준다.
    /// 문서가 없으면 -1.
    /// </summary>
    internal static long Set(string name, string xpath, string value, long doSetAll, long outputType)
    {
        if (!docs.TryGetValue(name ?? "", out var d)) return -1;
        var nodes = SelectNodes(d, xpath);
        if (nodes == null) return -1;
        long n = 0;
        foreach (XmlNode node in nodes)
        {
            if (!Assign(node, value, outputType)) continue;
            ++n;
            if (doSetAll == 0) break;
        }
        return n;
    }

    static bool Assign(XmlNode node, string value, long outputType)
    {
        try
        {
            switch (outputType)
            {
                case 1: node.InnerText = value; return true;
                case 2: node.InnerXml = value; return true;
                default:
                    // Value 는 속성·텍스트 노드에만 쓸 수 있다.
                    // 요소 노드라면 InnerText 로 넘어간다.
                    if (node.NodeType == XmlNodeType.Element)
                        node.InnerText = value;
                    else
                        node.Value = value;
                    return true;
            }
        }
        catch (Exception e)
        {
            uEmuera.Logger.Warn($"XML_SET: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// xpath 로 고른 노드에 nodeXml 을 붙인다.
    /// methodType 0=자식 끝에 추가, 1=자식 앞에 추가, 2=뒤 형제, 3=앞 형제.
    /// 추가한 개수. 문서가 없으면 -1.
    /// </summary>
    internal static long AddNode(string name, string xpath, string nodeXml,
        long methodType, long doSetAll)
    {
        if (!docs.TryGetValue(name ?? "", out var d)) return -1;
        var nodes = SelectNodes(d, xpath);
        if (nodes == null) return -1;
        long n = 0;
        foreach (XmlNode node in nodes)
        {
            XmlNode? made = MakeFragment(d, nodeXml);
            if (made == null) continue;
            try
            {
                switch (methodType)
                {
                    case 1: node.InsertBefore(made, node.FirstChild); break;
                    case 2: node.ParentNode?.InsertAfter(made, node); break;
                    case 3: node.ParentNode?.InsertBefore(made, node); break;
                    default: node.AppendChild(made); break;
                }
                ++n;
            }
            catch (Exception e)
            {
                uEmuera.Logger.Warn($"XML_ADDNODE: {e.Message}");
                continue;
            }
            if (doSetAll == 0) break;
        }
        return n;
    }

    /// <summary>xpath 로 고른 노드를 지운다. 지운 개수. 문서가 없으면 -1.</summary>
    internal static long RemoveNode(string name, string xpath, long doSetAll)
    {
        if (!docs.TryGetValue(name ?? "", out var d)) return -1;
        var nodes = SelectNodes(d, xpath);
        if (nodes == null) return -1;
        // 지우면서 순회하지 않도록 먼저 모아둔다.
        var targets = new List<XmlNode>();
        foreach (XmlNode node in nodes) targets.Add(node);
        long n = 0;
        foreach (var node in targets)
        {
            if (node.ParentNode == null) continue;
            node.ParentNode.RemoveChild(node);
            ++n;
            if (doSetAll == 0) break;
        }
        return n;
    }

    /// <summary>속성을 넣는다. 넣은 개수. 문서가 없으면 -1.</summary>
    internal static long AddAttribute(string name, string xpath,
        string attrName, string attrValue, long doSetAll)
    {
        if (!docs.TryGetValue(name ?? "", out var d)) return -1;
        var nodes = SelectNodes(d, xpath);
        if (nodes == null || string.IsNullOrEmpty(attrName)) return -1;
        long n = 0;
        foreach (XmlNode node in nodes)
        {
            if (node is not XmlElement el) continue;
            el.SetAttribute(attrName, attrValue ?? "");
            ++n;
            if (doSetAll == 0) break;
        }
        return n;
    }

    /// <summary>속성을 지운다. 지운 개수. 문서가 없으면 -1.</summary>
    internal static long RemoveAttribute(string name, string xpath,
        string attrName, long doSetAll)
    {
        if (!docs.TryGetValue(name ?? "", out var d)) return -1;
        var nodes = SelectNodes(d, xpath);
        if (nodes == null || string.IsNullOrEmpty(attrName)) return -1;
        long n = 0;
        foreach (XmlNode node in nodes)
        {
            if (node is not XmlElement el) continue;
            if (!el.HasAttribute(attrName)) continue;
            el.RemoveAttribute(attrName);
            ++n;
            if (doSetAll == 0) break;
        }
        return n;
    }

    // ------------------------------------------------------------------
    // 내부
    // ------------------------------------------------------------------

    static XmlDocument? Parse(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;
        var d = new XmlDocument();
        try
        {
            // 외부 엔티티 해석을 막는다. 게임 데이터는 신뢰 대상이 아니고,
            // XXE 로 로컬 파일을 읽히거나 멈추게 할 이유가 없다.
            d.XmlResolver = null;
            d.LoadXml(content);
            return d;
        }
        catch (Exception e)
        {
            uEmuera.Logger.Warn($"XML 파싱 실패: {e.Message}");
            return null;
        }
    }

    static XmlNodeList? SelectNodes(XmlDocument d, string xpath)
    {
        if (string.IsNullOrEmpty(xpath)) return null;
        try
        {
            return d.SelectNodes(xpath);
        }
        catch (Exception e)
        {
            uEmuera.Logger.Warn($"XPath('{xpath}'): {e.Message}");
            return null;
        }
    }

    static XmlNode? MakeFragment(XmlDocument d, string nodeXml)
    {
        if (string.IsNullOrEmpty(nodeXml)) return null;
        try
        {
            var frag = d.CreateDocumentFragment();
            frag.InnerXml = nodeXml;
            return frag;
        }
        catch (Exception e)
        {
            uEmuera.Logger.Warn($"XML 조각 생성 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>타이틀 복귀 / RESETDATA 시 호출.</summary>
    internal static void ClearAll() => docs.Clear();
}
