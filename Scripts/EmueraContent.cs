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

    Button? fontSmaller;
    Button? fontLarger;
    Label? fontValue;
    Label? fontSample;

    // メニューは PopupPanel(=Window派生)ではなく通常のControlオーバーレイで作る。
    // Windowノードは環境によって埋め込み(embed subwindow)の扱いが異なり、
    // Androidで生成に失敗すると起動時にそのまま落ちる。Visibleの切替だけで
    // 済むControlなら、その経路のリスクが無い。
    Control? menuLayer;
    Control? fontLayer;

    // 表示に使う文字サイズ。Settings経由で永続化される。
    int fontSize = Settings.DefaultFontSize;

    // 内容が変わった後、何フレーム最下部へ追従させるか。
    // RichTextLabel(FitContent)の高さは追加されたフレームでは確定せず、
    // スクロールバーの最大値も古いままなので、1回だけでは途中で止まる。
    int scrollFramesLeft;
    const int ScrollFollowFrames = 4;

    public override void _Ready()
    {
        // ScrollContainerとInputBarはLayout(VBoxContainer)の子。
        // 以前はScrollContainerが全画面、InputBarがその上に重なる構造で、
        // 画面下部の選択肢が入力欄に隠れて読めなかった。
        scrollContainer = GetNodeOrNull<ScrollContainer>("Layout/ScrollContainer");
        textContainer = scrollContainer?.GetNodeOrNull<VBoxContainer>("VBoxContainer");

        inputBar = GetNodeOrNull<Control>("Layout/InputBar");
        inputEdit = GetNodeOrNull<LineEdit>("Layout/InputBar/LineEdit");
        inputSubmit = GetNodeOrNull<Button>("Layout/InputBar/SubmitButton");

        processLabel = GetNodeOrNull<Label>("ProcessLabel");

        menuLayer = GetNodeOrNull<Control>("MenuLayer");
        fontLayer = GetNodeOrNull<Control>("FontLayer");

        const string fontRoot = "FontLayer/Center/Panel/VBox";
        fontSmaller = GetNodeOrNull<Button>(fontRoot + "/Row/SmallerButton");
        fontLarger = GetNodeOrNull<Button>(fontRoot + "/Row/LargerButton");
        fontValue = GetNodeOrNull<Label>(fontRoot + "/Row/FontValue");
        fontSample = GetNodeOrNull<Label>(fontRoot + "/Sample");

        if (inputEdit != null)
            inputEdit.TextSubmitted += _ => SubmitTypedInput();
        if (inputSubmit != null)
            inputSubmit.Pressed += SubmitTypedInput;

        Wire("MenuButton", () => SetLayerVisible(menuLayer, true));

        const string menuRoot = "MenuLayer/Center/Panel/VBox";
        Wire(menuRoot + "/RestartButton", OnRestart);
        Wire(menuRoot + "/SaveLogButton", OnSaveLog);
        Wire(menuRoot + "/FontButton", OnOpenFontSettings);
        Wire(menuRoot + "/QuitToListButton", OnQuitToList);
        Wire(menuRoot + "/QuitAppButton", OnQuitApp);
        Wire(menuRoot + "/CloseButton", () => SetLayerVisible(menuLayer, false));
        Wire(fontRoot + "/FontCloseButton", () => SetLayerVisible(fontLayer, false));

        if (fontSmaller != null)
            fontSmaller.Pressed += () => NudgeFontSize(-2);
        if (fontLarger != null)
            fontLarger.Pressed += () => NudgeFontSize(+2);

        fontSize = Settings.FontSize;
        ApplyFontSizeToChrome();

        SetLayerVisible(menuLayer, false);
        SetLayerVisible(fontLayer, false);

        if (inputBar != null)
            inputBar.Visible = false;
        if (processLabel != null)
            processLabel.Visible = false;
    }

    /// <summary>ノードが見つからなくても落ちないように包む。</summary>
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

    static void SetLayerVisible(Control? layer, bool visible)
    {
        if (layer != null)
            layer.Visible = visible;
    }

    // ------------------------------------------------------------------
    // メニュー
    // ------------------------------------------------------------------

    void OnRestart()
    {
        SetLayerVisible(menuLayer, false);
        GetNodeOrNull<EmueraMain>("../EmueraMain")?.Restart();
    }

    void OnQuitToList()
    {
        SetLayerVisible(menuLayer, false);
        GetNodeOrNull<EmueraMain>("../EmueraMain")?.Clear();
    }

    void OnQuitApp()
    {
        SetLayerVisible(menuLayer, false);
        // EmueraMain._Notificationがエンジンスレッドを畳んでから終了する。
        GetTree()?.Quit();
    }

    void OnOpenFontSettings()
    {
        SetLayerVisible(menuLayer, false);
        UpdateFontPopupLabels();
        SetLayerVisible(fontLayer, true);
    }

    void OnSaveLog()
    {
        SetLayerVisible(menuLayer, false);
        try
        {
            var path = SaveLog();
            if (path != null)
                ShowToast($"로그 저장: {path}");
        }
        catch (Exception e)
        {
            // ここで例外を漏らすとUIスレッドが死ぬので必ず止める
            GD.PushError($"SaveLog failed: {e}");
            ShowToast("로그 저장에 실패했습니다.");
        }
    }

    /// <summary>
    /// コンソール内容をテキストファイルに保存する。
    /// ゲームフォルダ配下のlogs/を優先し、書けない場合はアプリ専用領域に出す。
    /// (Androidのuser://はアプリ専用でファイル管理アプリから探しにくい)
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
            try
            {
                var dir = System.IO.Path.Combine(gameRoot, "logs");
                var full = System.IO.Path.Combine(dir, name);
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(full, sb.ToString(), new UTF8Encoding(true));
                return full;
            }
            catch (Exception e)
            {
                GD.PushWarning($"게임 폴더에 로그를 저장할 수 없습니다: {e.Message}");
            }
        }

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

    /// <summary>BBCodeを通さない素のテキスト(ログ保存用)。</summary>
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

    /// <summary>簡単な通知。ProcessLabelを一時的に借りる。</summary>
    void ShowToast(string msg)
    {
        GD.Print("[Kemura] " + msg);
        if (processLabel == null) return;
        processLabel.Text = msg;
        processLabel.Visible = true;
        toastFramesLeft = 240;   // 約4秒(60fps基準)
    }

    int toastFramesLeft;

    // ------------------------------------------------------------------
    // 文字サイズ
    // ------------------------------------------------------------------

    void NudgeFontSize(int delta)
    {
        int next = Mathf.Clamp(fontSize + delta, Settings.MinFontSize, Settings.MaxFontSize);
        if (next == fontSize)
            return;
        fontSize = next;
        Settings.FontSize = next;      // 永続化

        // 既に生成済みの行にも即座に反映する(作り直さずサイズだけ差し替える)
        foreach (var label in lineNodes)
            ApplyFontTo(label);
        ApplyFontSizeToChrome();
        UpdateFontPopupLabels();
        RequestScrollToBottom();       // 高さが変わるので追従し直す
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

    /// <summary>入力欄など本文以外のUIも同じサイズ感に揃える。</summary>
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

        // 表示サイズはEmueraのConfig.FontSizeではなくユーザー設定に従う。
        // Config.FontSizeはエンジン内部の折り返し計算用の値で、画面上の
        // 読みやすさ(特にスマートフォン)とは別問題。
        ApplyFontTo(label);

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

    /// <summary>
    /// 最新ログに追従して最下部へスクロールするよう要求する。
    ///
    /// 1回で足りない理由: RichTextLabelはFitContentなので追加されたフレームでは
    /// 高さがほぼ0で、ScrollContainerのスクロールバー最大値も未更新。
    /// そのため数フレームにわたって最下部へ押し続ける。
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
        if (toastFramesLeft > 0) return;   // 通知表示中は上書きしない
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
        RequestScrollToBottom();
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
        RequestScrollToBottom();
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
            RequestScrollToBottom();
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
                RequestScrollToBottom();
                AcceptEvent();
            }
        }
    }
}
