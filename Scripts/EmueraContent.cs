using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.GameView;

/// <summary>
/// Emuera 콘솔의 표시부.
///
/// Emuera 엔진은 전용 스레드에서 동작하므로 AddLine() 등은 그 스레드에서
/// 호출된다. Godot 씬 트리는 메인 스레드에서만 다룰 수 있으므로,
/// 받은 행을 일단 큐에 쌓고 CallDeferred로 메인 스레드에 넘겨
/// 거기서 노드를 생성한다.
/// </summary>
public partial class EmueraContent : Control
{
    VBoxContainer? textContainer;
    ScrollContainer? scrollContainer;
    LineEdit? inputEdit;
    Button? inputSubmit;
    Control? inputBar;
    Label? processLabel;

    Godot.Color backgroundColor = new Godot.Color(0, 0, 0, 1);

    // 메인 스레드 전용. lines와 lineNodes는 항상 같은 길이·같은 순서.
    readonly List<ConsoleDisplayLine> lines = new();
    readonly List<RichTextLabel> lineNodes = new();

    // Emuera 스레드 → 메인 스레드 전달용
    readonly ConcurrentQueue<ConsoleDisplayLine> pendingLines = new();

    bool isInProcess;
    int lastButtonGeneration = -1;

    const int MaxCachedLines = 2000;

    Button? fontSmaller;
    Button? fontLarger;
    Label? fontValue;
    Label? fontSample;

    Button? menuButton;
    PopupPanel? menuPopup;
    PopupPanel? fontPopup;

    // 표시에 사용할 글자 크기. Settings를 통해 영속화된다.
    int fontSize = Settings.DefaultFontSize;

    // 내용이 바뀐 뒤 몇 프레임 동안 최하단으로 계속 스크롤할지.
    // RichTextLabel(FitContent)의 높이는 추가된 프레임에 확정되지 않으므로
    // 한 번만 스크롤하면 스크롤바 최대값이 낡은 값이어서 끝까지 가지 않는다.
    int scrollFramesLeft;
    const int ScrollFollowFrames = 4;

    public override void _Ready()
    {
        // ScrollContainer와 InputBar는 Layout(VBoxContainer)의 자식.
        // 이전에는 ScrollContainer가 전체 화면이고 InputBar가 그 위에 겹쳐
        // 화면 하단의 선택지가 입력창에 가려 읽을 수 없었다.
        scrollContainer = GetNodeOrNull<ScrollContainer>("Layout/ScrollContainer");
        textContainer = scrollContainer?.GetNodeOrNull<VBoxContainer>("VBoxContainer");

        inputBar = GetNodeOrNull<Control>("Layout/InputBar");
        inputEdit = GetNodeOrNull<LineEdit>("Layout/InputBar/LineEdit");
        inputSubmit = GetNodeOrNull<Button>("Layout/InputBar/SubmitButton");

        processLabel = GetNodeOrNull<Label>("ProcessLabel");

        menuButton = GetNodeOrNull<Button>("MenuButton");
        menuPopup = GetNodeOrNull<PopupPanel>("MenuPopup");
        fontPopup = GetNodeOrNull<PopupPanel>("FontPopup");

        fontSmaller = GetNodeOrNull<Button>("FontPopup/VBox/Row/SmallerButton");
        fontLarger = GetNodeOrNull<Button>("FontPopup/VBox/Row/LargerButton");
        fontValue = GetNodeOrNull<Label>("FontPopup/VBox/Row/FontValue");
        fontSample = GetNodeOrNull<Label>("FontPopup/VBox/Sample");

        if (inputEdit != null)
            inputEdit.TextSubmitted += _ => SubmitTypedInput();
        if (inputSubmit != null)
            inputSubmit.Pressed += SubmitTypedInput;

        if (menuButton != null)
            menuButton.Pressed += () => menuPopup?.PopupCentered();

        Wire("MenuPopup/VBox/RestartButton", OnRestart);
        Wire("MenuPopup/VBox/SaveLogButton", OnSaveLog);
        Wire("MenuPopup/VBox/FontButton", OnOpenFontSettings);
        Wire("MenuPopup/VBox/QuitToListButton", OnQuitToList);
        Wire("MenuPopup/VBox/QuitAppButton", OnQuitApp);
        Wire("MenuPopup/VBox/CloseButton", () => menuPopup?.Hide());
        Wire("FontPopup/VBox/FontCloseButton", () => fontPopup?.Hide());

        if (fontSmaller != null)
            fontSmaller.Pressed += () => NudgeFontSize(-2);
        if (fontLarger != null)
            fontLarger.Pressed += () => NudgeFontSize(+2);

        fontSize = Settings.FontSize;
        ApplyFontSizeToChrome();

        if (inputBar != null)
            inputBar.Visible = false;
        if (processLabel != null)
            processLabel.Visible = false;
    }

    void Wire(string path, Action handler)
    {
        var b = GetNodeOrNull<Button>(path);
        if (b == null)
        {
            GD.PushWarning($"EmueraContent: 버튼을 찾을 수 없습니다 ({path})");
            return;
        }
        b.Pressed += handler;
    }

    // ------------------------------------------------------------------
    // 메뉴
    // ------------------------------------------------------------------

    void OnRestart()
    {
        menuPopup?.Hide();
        GetNodeOrNull<EmueraMain>("../EmueraMain")?.Restart();
    }

    void OnQuitToList()
    {
        menuPopup?.Hide();
        GetNodeOrNull<EmueraMain>("../EmueraMain")?.Clear();
    }

    void OnQuitApp()
    {
        menuPopup?.Hide();
        // 엔진 스레드를 정리한 뒤 종료한다(EmueraMain._Notification이 End를 호출).
        GetTree()?.Quit();
    }

    void OnOpenFontSettings()
    {
        menuPopup?.Hide();
        UpdateFontPopupLabels();
        fontPopup?.PopupCentered();
    }

    void OnSaveLog()
    {
        menuPopup?.Hide();
        var path = SaveLog();
        if (path != null)
            ShowToast($"로그를 저장했습니다:\n{path}");
    }

    /// <summary>
    /// 콘솔 내용을 텍스트 파일로 저장한다.
    /// 게임 폴더 아래 logs/ 를 우선 시도하고, 쓸 수 없으면 앱 전용 저장소로 보낸다.
    /// (Android에서 user:// 는 앱 전용이라 파일 관리자로 찾기 어렵다)
    /// </summary>
    string? SaveLog()
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.Append(BuildPlainText(line)).Append('\n');

        var name = $"kemura_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        var gameRoot = MinorShift._Library.Sys.ExeDir;
        if (!string.IsNullOrEmpty(gameRoot))
        {
            var dir = System.IO.Path.Combine(gameRoot, "logs");
            var full = System.IO.Path.Combine(dir, name);
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(full, sb.ToString(), new UTF8Encoding(true));
                return full;
            }
            catch (Exception e)
            {
                GD.PushWarning($"게임 폴더에 로그를 저장할 수 없습니다: {e.Message}");
            }
        }

        // 대체 경로: 앱 전용 저장소
        var userPath = "user://" + name;
        using var f = FileAccess.Open(userPath, FileAccess.ModeFlags.Write);
        if (f == null)
        {
            ShowToast($"로그를 저장할 수 없습니다 ({FileAccess.GetOpenError()})");
            return null;
        }
        f.StoreString(sb.ToString());
        return ProjectSettings.GlobalizePath(userPath);
    }

    /// <summary>BBCode를 거치지 않은 순수 텍스트(로그 저장용).</summary>
    static string BuildPlainText(ConsoleDisplayLine line)
    {
        var buttons = line.Buttons;
        if (buttons == null || buttons.Length == 0)
            return "";
        var sb = new StringBuilder();
        foreach (var button in buttons)
        {
            var parts = button?.StrArray;
            if (parts == null) continue;
            foreach (var part in parts)
            {
                if (part?.Str != null)
                    sb.Append(part.Str);
            }
        }
        return sb.ToString();
    }

    /// <summary>간단한 안내 표시. ProcessLabel을 잠시 빌려 쓴다.</summary>
    void ShowToast(string msg)
    {
        GD.Print("[Kemura] " + msg);
        if (processLabel == null) return;
        processLabel.Text = msg;
        processLabel.Visible = true;
        toastFramesLeft = 240;   // 약 4초(60fps 기준)
    }

    int toastFramesLeft;

    // ------------------------------------------------------------------
    // 글자 크기
    // ------------------------------------------------------------------

    void NudgeFontSize(int delta)
    {
        int next = Mathf.Clamp(fontSize + delta, Settings.MinFontSize, Settings.MaxFontSize);
        if (next == fontSize)
            return;
        fontSize = next;
        Settings.FontSize = next;      // 영속화

        // 이미 생성된 행에도 즉시 반영한다(다시 만들지 않고 크기만 교체)
        foreach (var label in lineNodes)
            ApplyFontTo(label);
        ApplyFontSizeToChrome();
        UpdateFontPopupLabels();
        RequestScrollToBottom();       // 크기가 바뀌면 높이도 바뀌므로 다시 맞춘다
    }

    void UpdateFontPopupLabels()
    {
        if (fontValue != null)
            fontValue.Text = fontSize.ToString();
        if (fontSample != null)
        {
            fontSample.AddThemeFontSizeOverride("font_size", fontSize);
            var font = FontUtils.GetFont();
            if (font != null)
                fontSample.AddThemeFontOverride("font", font);
        }
    }

    void ApplyFontTo(RichTextLabel label)
    {
        var font = FontUtils.GetFont();
        if (font != null)
            label.AddThemeFontOverride("normal_font", font);
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        label.AddThemeFontSizeOverride("italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("mono_font_size", fontSize);
    }

    /// <summary>입력창 등 본문 이외의 UI도 같은 크기감으로 맞춘다.</summary>
    void ApplyFontSizeToChrome()
    {
        var font = FontUtils.GetFont();
        if (inputEdit != null)
        {
            inputEdit.AddThemeFontSizeOverride("font_size", fontSize);
            if (font != null) inputEdit.AddThemeFontOverride("font", font);
        }
        if (inputSubmit != null)
        {
            inputSubmit.AddThemeFontSizeOverride("font_size", fontSize);
            if (font != null) inputSubmit.AddThemeFontOverride("font", font);
        }
    }

    // ------------------------------------------------------------------
    // Emuera 스레드에서 호출되는 입구
    // ------------------------------------------------------------------

    internal void AddLine(ConsoleDisplayLine line, bool old)
    {
        if (line == null) return;
        pendingLines.Enqueue(line);
        // 이전에는 Variant.From(line) 을 넘겼으나 ConsoleDisplayLine은
        // GodotObject가 아니라 Variant로 마샬링할 수 없다
        // ([MustBeVariant] 제약 위반). 큐를 경유해 전달한다.
        Callable.From(DrainPendingLines).CallDeferred();
    }

    public void Clear()
    {
        while (pendingLines.TryDequeue(out _)) { }
        Callable.From(ClearDeferred).CallDeferred();
    }

    public void UpdateDisplay()
    {
        Callable.From(DrainPendingLines).CallDeferred();
    }

    public void SetBackgroundColor(uEmuera.Drawing.Color c)
    {
        var gc = new Godot.Color(c.r, c.g, c.b, c.a);
        if (gc == backgroundColor) return;
        backgroundColor = gc;
        Callable.From(QueueRedraw).CallDeferred();
    }

    public void ShowIsInProcess(bool show)
    {
        if (isInProcess == show) return;
        isInProcess = show;
        Callable.From(ApplyProcessIndicator).CallDeferred();
    }

    public void SetLastButtonGeneration(int gen) => lastButtonGeneration = gen;

    // ------------------------------------------------------------------
    // 행 번호 장부
    //   GetMaxLineNo() 는 '마지막 행의 LineNo + 1' (배타적 상한)
    //   GetMinLineNo() 는 보유 중인 최소 LineNo
    // Window.Update 의 차분 알고리즘이 이 의미를 전제로 한다.
    // ------------------------------------------------------------------

    public int GetMinLineNo() => lines.Count > 0 ? lines[0].LineNo : 0;
    public int GetMaxLineNo() => lines.Count > 0 ? lines[lines.Count - 1].LineNo + 1 : 0;

    internal ConsoleDisplayLine? GetLine(int lineNo)
    {
        if (lines.Count == 0) return null;
        // LineNo가 연속인 일반적인 경우는 인덱스로 바로 찾는다
        int guess = lineNo - lines[0].LineNo;
        if (guess >= 0 && guess < lines.Count && lines[guess].LineNo == lineNo)
            return lines[guess];
        // 줄바꿈 등으로 연속성이 깨진 경우는 끝에서부터 탐색한다
        for (int i = lines.Count - 1; i >= 0; --i)
        {
            if (lines[i].LineNo == lineNo)
                return lines[i];
        }
        return null;
    }

    public void RemoveLines(int count)
    {
        if (count <= 0 || lines.Count == 0) return;
        int removeCount = Math.Min(count, lines.Count);
        int start = lines.Count - removeCount;

        for (int i = start; i < lineNodes.Count; ++i)
            lineNodes[i].QueueFree();

        lines.RemoveRange(start, removeCount);
        lineNodes.RemoveRange(start, removeCount);
    }

    // ------------------------------------------------------------------
    // 메인 스레드 쪽 실제 처리
    // ------------------------------------------------------------------

    void DrainPendingLines()
    {
        if (textContainer == null) return;

        bool added = false;
        while (pendingLines.TryDequeue(out var line))
        {
            AppendLineNode(line);
            added = true;
        }
        if (!added) return;

        TrimToCap();
        RequestScrollToBottom();
    }

    void AppendLineNode(ConsoleDisplayLine line)
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SelectionEnabled = true,
            MouseFilter = MouseFilterEnum.Pass,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        // 표시 크기는 Emuera의 Config.FontSize가 아니라 사용자 설정에 따른다.
        // Config.FontSize는 엔진 내부의 줄바꿈 계산용 값이라 화면에서의
        // 가독성(특히 스마트폰)과는 별개 문제다.
        ApplyFontTo(label);

        label.Text = BuildBbcode(line);
        // 버튼 부분은 [url=...] 로 감싸므로 meta_clicked 로 입력으로 변환한다
        label.MetaClicked += meta => OnButtonClicked(meta.AsString());

        textContainer!.AddChild(label);
        lines.Add(line);
        lineNodes.Add(label);
    }

    void TrimToCap()
    {
        int overflow = lines.Count - MaxCachedLines;
        if (overflow <= 0) return;
        for (int i = 0; i < overflow; ++i)
            lineNodes[i].QueueFree();
        lines.RemoveRange(0, overflow);
        lineNodes.RemoveRange(0, overflow);
    }

    void ClearDeferred()
    {
        foreach (var node in lineNodes)
            node.QueueFree();
        lineNodes.Clear();
        lines.Clear();
    }

    /// <summary>
    /// 최신 로그를 따라 최하단으로 스크롤하도록 요청한다.
    ///
    /// 한 번만 스크롤하면 안 되는 이유: RichTextLabel은 FitContent이므로 추가된
    /// 프레임에는 높이가 0에 가깝고, ScrollContainer의 스크롤바 최대값도 아직
    /// 갱신되지 않았다. 그래서 몇 프레임 동안 계속 최하단으로 밀어준다.
    /// </summary>
    void RequestScrollToBottom()
    {
        scrollFramesLeft = ScrollFollowFrames;
    }

    void ScrollToBottom()
    {
        if (scrollContainer == null) return;
        var vbar = scrollContainer.GetVScrollBar();
        if (vbar == null) return;
        scrollContainer.ScrollVertical = (int)(vbar.MaxValue - vbar.Page);
    }

    public override void _Process(double delta)
    {
        if (scrollFramesLeft > 0)
        {
            --scrollFramesLeft;
            ScrollToBottom();
        }

        if (toastFramesLeft > 0)
        {
            --toastFramesLeft;
            if (toastFramesLeft == 0 && processLabel != null)
            {
                processLabel.Text = "처리 중...";
                processLabel.Visible = isInProcess;
            }
        }
    }

    void ApplyProcessIndicator()
    {
        // 안내 메시지(토스트)를 표시하는 중이면 덮어쓰지 않는다
        if (toastFramesLeft > 0) return;
        if (processLabel != null)
            processLabel.Visible = isInProcess;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), backgroundColor);
    }

    // ------------------------------------------------------------------
    // BBCode 생성
    // ------------------------------------------------------------------

    static string BuildBbcode(ConsoleDisplayLine line)
    {
        var buttons = line.Buttons;
        if (buttons == null || buttons.Length == 0)
            return "";

        var sb = new StringBuilder();

        switch (line.Align)
        {
            case DisplayLineAlignment.CENTER: sb.Append("[center]"); break;
            case DisplayLineAlignment.RIGHT: sb.Append("[right]"); break;
        }

        foreach (var button in buttons)
        {
            if (button == null) continue;
            var parts = button.StrArray;
            if (parts == null || parts.Length == 0) continue;

            bool clickable = button.IsButton && !string.IsNullOrEmpty(button.Inputs);
            if (clickable)
            {
                sb.Append("[url=").Append(EscapeBbcode(button.Inputs)).Append(']');
                sb.Append("[u]");
            }

            foreach (var part in parts)
            {
                if (part == null) continue;
                var text = part.Str;
                if (string.IsNullOrEmpty(text)) continue;

                var colored = part as AConsoleColoredPart;
                if (colored != null)
                {
                    var c = clickable ? colored.pButtonColor : colored.pColor;
                    sb.Append("[color=#").Append(ToHex(c)).Append(']');
                    sb.Append(EscapeBbcode(text));
                    sb.Append("[/color]");
                }
                else
                {
                    sb.Append(EscapeBbcode(text));
                }
            }

            if (clickable)
            {
                sb.Append("[/u]");
                sb.Append("[/url]");
            }
        }

        switch (line.Align)
        {
            case DisplayLineAlignment.CENTER: sb.Append("[/center]"); break;
            case DisplayLineAlignment.RIGHT: sb.Append("[/right]"); break;
        }

        return sb.ToString();
    }

    static string ToHex(uEmuera.Drawing.Color c)
        => $"{Mathf.Clamp(c.R, 0, 255):x2}{Mathf.Clamp(c.G, 0, 255):x2}{Mathf.Clamp(c.B, 0, 255):x2}";

    /// <summary>BBCode로 해석되지 않도록 '[' 를 이스케이프한다.</summary>
    static string EscapeBbcode(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("[", "[lb]");

    // ------------------------------------------------------------------
    // 입력
    // ------------------------------------------------------------------

    void OnButtonClicked(string inputs)
    {
        if (string.IsNullOrEmpty(inputs)) return;
        EmueraThread.instance.Input(inputs, true);
        // 선택 직후 최신 로그를 따라 최하단으로 내려간다.
        // 새 행이 도착하면 DrainPendingLines가 다시 요청하므로 중복돼도 무해하다.
        RequestScrollToBottom();
    }

    /// <summary>
    /// 숫자/문자열 입력 대기 상태에서만 입력창을 띄운다. EmueraMain._Process에서 매 프레임 호출된다.
    /// </summary>
    internal void SyncInputBar()
    {
        var console = GlobalStatic.Console;
        bool want = console != null && console.IsWaitingInputSomething;
        if (inputBar == null || inputBar.Visible == want)
            return;

        inputBar.Visible = want;
        if (want && inputEdit != null)
        {
            var isInt = console!.InputType == MinorShift.Emuera.GameProc.InputType.IntValue;
            inputEdit.PlaceholderText = isInt ? "숫자 입력" : "문자 입력";
            inputEdit.Text = "";
            inputEdit.GrabFocus();
        }
    }

    void SubmitTypedInput()
    {
        if (inputEdit == null) return;
        var text = inputEdit.Text;
        var console = GlobalStatic.Console;
        if (console != null &&
            console.InputType == MinorShift.Emuera.GameProc.InputType.IntValue &&
            !long.TryParse(text, out _))
        {
            // 숫자 입력 대기에 비숫자를 보내면 Emuera가 무시해 입력이 사라지므로 보내지 않는다
            inputEdit.PlaceholderText = "숫자를 입력해주세요";
            return;
        }
        inputEdit.Text = "";
        EmueraThread.instance.Input(text, true);
        RequestScrollToBottom();
    }

    /// <summary>
    /// 탭/클릭으로 '다음으로 진행'. 이전에는 InputEventScreenTouch만 처리해서
    /// 데스크톱(마우스)에서는 전혀 조작할 수 없었다.
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        bool advance =
            (@event is InputEventScreenTouch touch && touch.Pressed) ||
            (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left);

        if (!advance) return;

        var console = GlobalStatic.Console;
        if (console == null) return;

        // 숫자/문자열 입력 대기 중에는 탭으로 진행하면 안 된다(입력창을 사용)
        if (console.IsWaitingInputSomething) return;

        if (console.IsWaitingEnterKey)
        {
            EmueraThread.instance.Input("", false);
            RequestScrollToBottom();
            AcceptEvent();
        }
    }

    /// <summary>Enter/Space로도 진행할 수 있게 한다(데스크톱·물리 키보드용).</summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        var console = GlobalStatic.Console;
        if (console == null || console.IsWaitingInputSomething)
            return;

        if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space)
        {
            if (console.IsWaitingEnterKey)
            {
                // Shift/Ctrl 누른 상태면 스킵으로 처리
                bool skip = key.ShiftPressed || key.CtrlPressed;
                EmueraThread.instance.Input("", false, skip);
                RequestScrollToBottom();
                AcceptEvent();
            }
        }
    }
}
