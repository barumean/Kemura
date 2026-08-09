/// <summary>
/// 입력창과 버튼 호버 상태를 엔진 스레드와 주고받는 다리.
///
/// EM+EE 의 <c>GETTEXTBOX</c> / <c>SETTEXTBOX</c> / <c>MOUSEB</c> 가 쓴다.
///
/// <b>왜 다리가 필요한가</b> — 입력창은 Godot 노드(<c>LineEdit</c>)이고 Godot
/// 노드는 메인 스레드에서만 만질 수 있다. era 명령은 엔진 스레드에서 실행되므로
/// 엔진이 노드를 직접 읽거나 쓰면 안 된다. 그래서 값만 주고받는다.
///
///  - 읽기(GETTEXTBOX, MOUSEB): UI 가 바뀔 때마다 여기에 문자열을 넣어두고
///    엔진은 그 스냅샷을 읽는다.
///  - 쓰기(SETTEXTBOX): 엔진이 요청을 남기고, UI 가 다음 프레임에 가져가
///    실제 노드에 반영한다.
///
/// 참조형 필드의 대입·읽기는 원자적이므로 락이 필요하지 않다. 한 프레임
/// 늦게 보이는 것은 이 용도에서 문제가 되지 않는다.
/// </summary>
internal static class EmTextBox
{
    /// <summary>입력창에 현재 들어 있는 문자열. UI 가 갱신한다.</summary>
    static volatile string current = "";

    /// <summary>엔진이 넣어달라고 요청한 문자열. UI 가 가져가면 null 로 되돌린다.</summary>
    static volatile string? pendingSet;

    /// <summary>지금 마우스가 올라가 있는 버튼의 내용. 없으면 빈 문자열.</summary>
    static volatile string hovered = "";

    // ------------------------------------------------------------------
    // 엔진 스레드에서 부르는 것
    // ------------------------------------------------------------------

    /// <summary>GETTEXTBOX.</summary>
    internal static string Get() => current;

    /// <summary>SETTEXTBOX. 실제 반영은 UI 가 다음 프레임에 한다.</summary>
    internal static void RequestSet(string text) => pendingSet = text ?? "";

    /// <summary>
    /// MOUSEB. 규격상 "올라가 있는 버튼의 내용"이다.
    ///
    /// 터치 화면에는 호버가 없으므로 손가락으로 조작할 때는 대개 빈 문자열이
    /// 된다. 그게 맞는 값이다 — 아무것도 올라가 있지 않으니까. 마우스나
    /// 스타일러스를 쓰면 실제 값이 들어온다.
    /// </summary>
    internal static string GetHovered() => hovered;

    // ------------------------------------------------------------------
    // Godot 메인 스레드에서 부르는 것
    // ------------------------------------------------------------------

    /// <summary>입력창 내용이 바뀌었을 때.</summary>
    internal static void OnTextChanged(string text) => current = text ?? "";

    /// <summary>
    /// 엔진이 요청한 문자열을 가져간다. 없으면 null.
    /// 가져가는 순간 요청을 비우므로 두 번 반영되지 않는다.
    /// </summary>
    internal static string? TakePendingSet()
    {
        var p = pendingSet;
        if (p == null)
            return null;
        pendingSet = null;
        // 스냅샷도 같이 맞춘다. 이걸 빼면 SETTEXTBOX 직후의 GETTEXTBOX 가
        // 한 프레임 동안 옛 값을 돌려준다.
        current = p;
        return p;
    }

    internal static void SetHovered(string meta) => hovered = meta ?? "";

    internal static void ClearHovered() => hovered = "";

    /// <summary>게임을 다시 시작할 때 등.</summary>
    internal static void Reset()
    {
        current = "";
        pendingSet = null;
        hovered = "";
    }
}
