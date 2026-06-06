using Godot;
using System;
using System.Collections.Generic;
using MinorShift.Emuera.GameView;

public partial class EmueraContent : Control
{
    VBoxContainer? textContainer;
    ScrollContainer? scrollContainer;
    Godot.Color backgroundColor = new Godot.Color(0, 0, 0, 1);

    readonly List<DisplayLine> lines = new();
    int minLineNo;
    int maxLineNo;

    bool isInProcess;
    int lastButtonGeneration = -1;

    const int MaxCachedLines = 2000;

    public override void _Ready()
    {
        scrollContainer = GetNode<ScrollContainer>("ScrollContainer");
        textContainer = scrollContainer?.GetNode<VBoxContainer>("VBoxContainer");
    }

    public void AddLine(DisplayLine line, bool old)
    {
        CallDeferred(MethodName.AddLineDeferred, Variant.From(line));
    }

    void AddLineDeferred(Variant lineVariant)
    {
        // Render logic deferred to main thread
        UpdateDisplay();
    }

    public void Clear()
    {
        lines.Clear();
        minLineNo = 0;
        maxLineNo = 0;
        CallDeferred(MethodName.ClearDeferred);
    }

    void ClearDeferred()
    {
        if (textContainer == null) return;
        foreach (Node child in textContainer.GetChildren())
            child.QueueFree();
    }

    public void UpdateDisplay()
    {
        CallDeferred(MethodName.RebuildDisplay);
    }

    void RebuildDisplay()
    {
        // Trigger redraw
        QueueRedraw();
    }

    public void SetBackgroundColor(uEmuera.Drawing.Color c)
    {
        backgroundColor = new Godot.Color(c.r, c.g, c.b, c.a);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), backgroundColor);
    }

    public void ShowIsInProcess(bool show)
    {
        isInProcess = show;
    }

    public void SetLastButtonGeneration(int gen)
    {
        lastButtonGeneration = gen;
    }

    public int GetMaxLineNo() => maxLineNo;
    public int GetMinLineNo() => minLineNo;

    public DisplayLine? GetLine(int lineNo)
    {
        int idx = lineNo - minLineNo;
        if (idx < 0 || idx >= lines.Count) return null;
        return lines[idx];
    }

    public void RemoveLines(int count)
    {
        if (count <= 0) return;
        int removeCount = Math.Min(count, lines.Count);
        lines.RemoveRange(lines.Count - removeCount, removeCount);
        maxLineNo -= removeCount;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            var console = MinorShift.Emuera.GlobalStatic.Console;
            if (console == null) return;

            // Tap to advance
            if (console.IsWaitingEnterKey)
                EmueraThread.instance.Input("", false, false);
        }
    }
}
