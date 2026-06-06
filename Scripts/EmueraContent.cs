using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Config;

/// <summary>
/// Emuera 텍스트 출력 렌더러.
/// 엔진 스레드 → ConcurrentQueue → 메인 스레드 Label 생성
/// </summary>
public partial class EmueraContent : Control
{
    // ── 노드 참조 ──────────────────────────────────────────────────
    VBoxContainer? textContainer;
    ScrollContainer? scrollContainer;

    // ── 배경색 (스레드에서 설정, 메인 스레드에서 읽음) ──────────
    Godot.Color backgroundColor = new Godot.Color(0, 0, 0, 1);

    // ── 라인 데이터 (메인 스레드 전용) ──────────────────────────
    readonly List<ConsoleDisplayLine> lines = new();
    int minLineNo;
    int maxLineNo;

    // ── 스레드 안전 큐 ──────────────────────────────────────────
    readonly ConcurrentQueue<(ConsoleDisplayLine line, bool old)> addQueue = new();
    volatile bool pendingClear;
    volatile bool pendingRedraw;

    // ── 스레드 안전 단순 상태 ───────────────────────────────────
    volatile bool isInProcess;
    volatile int lastButtonGeneration = -1;

    const int MaxCachedLines = 2000;

    // ── 표시 파라미터 ───────────────────────────────────────────
    int fontSize = 18;
    Godot.Color foreColor = new Godot.Color(0.75f, 0.75f, 0.75f);
    Godot.Color focusColor = new Godot.Color(1f, 1f, 0f);

    public override void _Ready()
    {
        scrollContainer = GetNode<ScrollContainer>("ScrollContainer");
        textContainer = scrollContainer?.GetNode<VBoxContainer>("VBoxContainer");

        // Config 로드 후 색상/폰트 갱신
        RefreshTheme();
    }

    void RefreshTheme()
    {
        try
        {
            var fg = MinorShift.Emuera.Config.Config.ForeColor;
            foreColor = new Godot.Color(fg.r, fg.g, fg.b, fg.a);
            var bg = MinorShift.Emuera.Config.Config.BackColor;
            backgroundColor = new Godot.Color(bg.r, bg.g, bg.b, bg.a);
            var fc = MinorShift.Emuera.Config.Config.FocusColor;
            focusColor = new Godot.Color(fc.r, fc.g, fc.b, fc.a);
            fontSize = MinorShift.Emuera.Config.Config.LineHeight;
            if (fontSize < 8) fontSize = 18;
        }
        catch { /* Config 미초기화 시 기본값 유지 */ }
    }

    // ── 엔진 스레드에서 호출되는 메서드 ─────────────────────────

    public void AddLine(ConsoleDisplayLine line, bool old)
    {
        addQueue.Enqueue((line, old));
        pendingRedraw = true;
    }

    public void Clear()
    {
        pendingClear = true;
    }

    public void UpdateDisplay()
    {
        pendingRedraw = true;
    }

    /// <summary>스레드 안전: 배경색 설정</summary>
    public void SetBackgroundColor(uEmuera.Drawing.Color c)
    {
        backgroundColor = new Godot.Color(c.r, c.g, c.b, c.a);
        pendingRedraw = true;
    }

    public void ShowIsInProcess(bool show)   => isInProcess = show;
    public void SetLastButtonGeneration(int gen) => lastButtonGeneration = gen;

    // ── 메인 스레드 전용 (Window.cs가 호출, 엔진 스레드 컨텍스트이므로 lock) ──
    readonly object _linesLock = new object();

    public int GetMaxLineNo() { lock (_linesLock) return maxLineNo; }
    public int GetMinLineNo() { lock (_linesLock) return minLineNo; }

    public ConsoleDisplayLine? GetLine(int lineNo)
    {
        lock (_linesLock)
        {
            int idx = lineNo - minLineNo;
            if (idx < 0 || idx >= lines.Count) return null;
            return lines[idx];
        }
    }

    public void RemoveLines(int count)
    {
        if (count <= 0) return;
        lock (_linesLock)
        {
            int removeCount = Math.Min(count, lines.Count);
            lines.RemoveRange(lines.Count - removeCount, removeCount);
            maxLineNo -= removeCount;
        }
        pendingRedraw = true;
    }

    // ── _Process: 메인 스레드에서 큐 드레인 ─────────────────────

    public override void _Process(double delta)
    {
        bool changed = false;

        // 클리어 요청
        if (pendingClear)
        {
            pendingClear = false;
            lock (_linesLock)
            {
                lines.Clear();
                minLineNo = 0;
                maxLineNo = 0;
            }
            DoClearNodes();
            RefreshTheme();
            changed = true;
        }

        // 큐에서 라인 드레인
        int drained = 0;
        while (addQueue.TryDequeue(out var item) && drained < 200)
        {
            lock (_linesLock)
            {
                lines.Add(item.line);
                if (lines.Count == 1)
                    minLineNo = item.line.LineNo;
                maxLineNo = item.line.LineNo;

                // 캐시 한계 초과 시 오래된 라인 제거
                if (lines.Count > MaxCachedLines)
                {
                    lines.RemoveAt(0);
                    minLineNo++;
                }
            }
            AppendLineNode(item.line);
            drained++;
            changed = true;
        }

        if (pendingRedraw || changed)
        {
            pendingRedraw = false;
            ScrollToBottom();
            QueueRedraw();
        }
    }

    // ── 노드 생성 ────────────────────────────────────────────────

    void AppendLineNode(ConsoleDisplayLine line)
    {
        if (textContainer == null) return;

        // 라인 전체 텍스트 + 버튼 구분 생성
        var hbox = new HBoxContainer();
        hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // 정렬
        if (line.Align == DisplayLineAlignment.CENTER)
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
        else if (line.Align == DisplayLineAlignment.RIGHT)
            hbox.Alignment = BoxContainer.AlignmentMode.End;
        else
            hbox.Alignment = BoxContainer.AlignmentMode.Begin;

        foreach (var btn in line.Buttons)
        {
            // 버튼 텍스트 조합
            var sb = new StringBuilder();
            foreach (var part in btn.StrArray)
                sb.Append(part.Str ?? "");
            string text = sb.ToString();
            if (string.IsNullOrEmpty(text)) continue;

            if (btn.IsButton)
            {
                var b = new Button();
                b.Text = text;
                b.AutowrapMode = TextServer.AutowrapMode.Off;
                b.AddThemeFontSizeOverride("font_size", fontSize);
                b.AddThemeColorOverride("font_color", focusColor);
                b.Flat = true;
                // 버튼 값을 클로저로 캡처
                string inputVal = btn.Inputs ?? btn.Input.ToString();
                b.Pressed += () => EmueraThread.instance.Input(inputVal, true);
                hbox.AddChild(b);
            }
            else
            {
                // 일반 텍스트
                var lbl = new Label();
                lbl.Text = text;
                lbl.AutowrapMode = TextServer.AutowrapMode.Off;
                lbl.AddThemeFontSizeOverride("font_size", fontSize);

                // ConsoleStyledString의 색상 적용
                // AConsoleColoredPart.pColor (public accessor) 로 색상 가져오기
                if (btn.StrArray.Length > 0 &&
                    btn.StrArray[0] is MinorShift.Emuera.GameView.AConsoleColoredPart colored)
                {
                    var c = colored.pColor;
                    lbl.AddThemeColorOverride("font_color", new Godot.Color(c.r, c.g, c.b));
                }
                else
                {
                    lbl.AddThemeColorOverride("font_color", foreColor);
                }

                hbox.AddChild(lbl);
            }
        }

        textContainer.AddChild(hbox);
    }

    void DoClearNodes()
    {
        if (textContainer == null) return;
        foreach (Node child in textContainer.GetChildren())
            child.QueueFree();
    }

    void ScrollToBottom()
    {
        if (scrollContainer == null) return;
        scrollContainer.ScrollVertical = (int)scrollContainer.GetVScrollBar().MaxValue;
    }

    // ── 입력 처리 ────────────────────────────────────────────────

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            var console = MinorShift.Emuera.GlobalStatic.Console;
            if (console == null) return;
            if (console.IsWaitingEnterKey)
                EmueraThread.instance.Input("", false, false);
        }
    }

    // ── 배경 그리기 ──────────────────────────────────────────────

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), backgroundColor);
    }
}
