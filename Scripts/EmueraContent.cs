using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.GameView;

/// <summary>
/// Emueraコンソールの表示部。
///
/// Emueraエンジンは専用スレッドで動作するため、AddLine()等はそのスレッドから
/// 呼ばれる。Godotのシーンツリーはメインスレッドからしかいじれないので、
/// 受け取った行はいったんキューに積み、CallDeferredでメインスレッドに渡して
/// そこでノードを生成する。
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

    // メインスレッド専用。linesとlineNodesは常に同じ長さ・同じ順序。
    readonly List<ConsoleDisplayLine> lines = new();
    readonly List<RichTextLabel> lineNodes = new();

    // Emueraスレッド → メインスレッドの受け渡し用
    readonly ConcurrentQueue<ConsoleDisplayLine> pendingLines = new();

    bool isInProcess;
    int lastButtonGeneration = -1;

    const int MaxCachedLines = 2000;

    public override void _Ready()
    {
        scrollContainer = GetNodeOrNull<ScrollContainer>("ScrollContainer");
        textContainer = scrollContainer?.GetNodeOrNull<VBoxContainer>("VBoxContainer");

        inputBar = GetNodeOrNull<Control>("InputBar");
        inputEdit = GetNodeOrNull<LineEdit>("InputBar/LineEdit");
        inputSubmit = GetNodeOrNull<Button>("InputBar/SubmitButton");
        processLabel = GetNodeOrNull<Label>("ProcessLabel");

        if (inputEdit != null)
            inputEdit.TextSubmitted += _ => SubmitTypedInput();
        if (inputSubmit != null)
            inputSubmit.Pressed += SubmitTypedInput;

        if (inputBar != null)
            inputBar.Visible = false;
        if (processLabel != null)
            processLabel.Visible = false;
    }

    // ------------------------------------------------------------------
    // Emueraスレッドから呼ばれる入口
    // ------------------------------------------------------------------

    internal void AddLine(ConsoleDisplayLine line, bool old)
    {
        if (line == null) return;
        pendingLines.Enqueue(line);
        // 以前は Variant.From(line) を渡していたが、ConsoleDisplayLineは
        // GodotObjectではないためVariantにマーシャリングできない
        // ([MustBeVariant]制約違反)。キュー経由で受け渡す。
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
    // 行番号の帳簿
    //   GetMaxLineNo() は「最後の行のLineNo + 1」(排他的上限)
    //   GetMinLineNo() は保持している最小のLineNo
    // Window.Updateの差分アルゴリズムがこの意味を前提にしている。
    // ------------------------------------------------------------------

    public int GetMinLineNo() => lines.Count > 0 ? lines[0].LineNo : 0;
    public int GetMaxLineNo() => lines.Count > 0 ? lines[lines.Count - 1].LineNo + 1 : 0;

    internal ConsoleDisplayLine? GetLine(int lineNo)
    {
        if (lines.Count == 0) return null;
        // LineNoが連番である通常ケースは直接添字で当てる
        int guess = lineNo - lines[0].LineNo;
        if (guess >= 0 && guess < lines.Count && lines[guess].LineNo == lineNo)
            return lines[guess];
        // 折り返し行などで連番が崩れている場合は末尾から探す
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
    // メインスレッド側の実処理
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
        // 追加直後はレイアウトが未確定なので、1フレーム待ってから最下部へ送る
        Callable.From(ScrollToBottom).CallDeferred();
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

        var font = FontUtils.GetFont();
        if (font != null)
            label.AddThemeFontOverride("normal_font", font);
        int fontSize = Config.FontSize > 0 ? Config.FontSize : 20;
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);

        label.Text = BuildBbcode(line);
        // ボタン部分は [url=...] で囲んでいるので meta_clicked で入力に変換する
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

    void ScrollToBottom()
    {
        if (scrollContainer == null) return;
        var vbar = scrollContainer.GetVScrollBar();
        if (vbar != null)
            scrollContainer.ScrollVertical = (int)vbar.MaxValue;
    }

    void ApplyProcessIndicator()
    {
        if (processLabel != null)
            processLabel.Visible = isInProcess;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), backgroundColor);
    }

    // ------------------------------------------------------------------
    // BBCode生成
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

    /// <summary>BBCodeとして解釈されないよう '[' をエスケープする。</summary>
    static string EscapeBbcode(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("[", "[lb]");

    // ------------------------------------------------------------------
    // 入力
    // ------------------------------------------------------------------

    void OnButtonClicked(string inputs)
    {
        if (string.IsNullOrEmpty(inputs)) return;
        EmueraThread.instance.Input(inputs, true);
    }

    /// <summary>
    /// 数値/文字列入力待ちのときだけ入力欄を出す。EmueraMain._Processから毎フレーム呼ばれる。
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
            inputEdit.PlaceholderText = isInt ? "数値を入力" : "文字を入力";
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
            // 数値入力待ちに非数値を送るとEmuera側で弾かれ入力が失われるので出さない
            inputEdit.PlaceholderText = "数値を入力してください";
            return;
        }
        inputEdit.Text = "";
        EmueraThread.instance.Input(text, true);
    }

    /// <summary>
    /// タップ/クリックで「次へ進む」。以前はInputEventScreenTouchのみ見ていたため
    /// デスクトップ(マウス)では一切操作できなかった。
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        bool advance =
            (@event is InputEventScreenTouch touch && touch.Pressed) ||
            (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left);

        if (!advance) return;

        var console = GlobalStatic.Console;
        if (console == null) return;

        // 数値/文字列入力待ちのときはタップで進めてはいけない(入力欄を使う)
        if (console.IsWaitingInputSomething) return;

        if (console.IsWaitingEnterKey)
        {
            EmueraThread.instance.Input("", false);
            AcceptEvent();
        }
    }

    /// <summary>Enter/Spaceでも進めるようにする(デスクトップ・物理キーボード用)。</summary>
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
                // Shift/Ctrl押下中はスキップ扱い
                bool skip = key.ShiftPressed || key.CtrlPressed;
                EmueraThread.instance.Input("", false, skip);
                AcceptEvent();
            }
        }
    }
}
