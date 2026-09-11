using Object1688.Shared.Ui;

namespace Object1688.Tests;

/// <summary>
/// UndoRedoStack 撤销/重做栈测试（需求书 AC-64）。
/// </summary>
public class UndoRedoStackTests
{
    [Fact]
    public void Push_AddsUndoDepth_ClearsRedo()
    {
        var stack = new UndoRedoStack<int>();
        stack.Push(1);
        stack.Push(2);
        Assert.Equal(2, stack.UndoDepth);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Undo_ReturnsPreviousState()
    {
        var stack = new UndoRedoStack<int>();
        stack.Push(1);
        stack.Push(2);
        var prev = stack.Undo();
        Assert.Equal(1, prev);
        Assert.Equal(1, stack.UndoDepth);
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public void Redo_ReturnsNextState_AfterUndo()
    {
        var stack = new UndoRedoStack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Undo();
        var next = stack.Redo();
        Assert.Equal(2, next);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Undo_AfterNewPush_InvalidatesRedo()
    {
        var stack = new UndoRedoStack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Undo();       // 回到 1，重做侧有 2
        stack.Push(3);      // 新编辑 → 清空重做侧
        Assert.False(stack.CanRedo);
        var prev = stack.Undo(); // 1
        Assert.Equal(1, prev);
    }

    [Fact]
    public void Undo_WhenEmpty_ReturnsDefault()
    {
        var stack = new UndoRedoStack<int>();
        Assert.Equal(0, stack.Undo()); // default(int)
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void Push_BeyondCapacity_DropsOldest()
    {
        var stack = new UndoRedoStack<int>(capacity: 2);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        Assert.Equal(2, stack.UndoDepth);
        Assert.Equal(2, stack.Undo()); // 最新上一态为 2（1 已被丢弃）
    }

    [Fact]
    public void Clear_ResetsBothSides()
    {
        var stack = new UndoRedoStack<int>();
        stack.Push(1);
        stack.Push(2);
        stack.Undo();
        stack.Clear();
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal(0, stack.UndoDepth);
        Assert.Equal(0, stack.RedoDepth);
    }
}
