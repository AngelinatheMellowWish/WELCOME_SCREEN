namespace Object1688.Shared.Ui;

/// <summary>
/// 撤销/重做栈（需求书 AC-64 / 架构 §5.6：编辑对象级 Ctrl+Z/Y；保存/切换对象时由调用方清空）。
/// 泛型命令栈：每个命令为可逆快照 T；Push 记录新态并清空重做侧，Undo 回退、Redo 前推，
/// 容量上限（超出丢弃最旧）。线程安全。
/// </summary>
/// <typeparam name="T">快照类型（应不可变或拷贝语义，调用方负责存副本）。</typeparam>
public sealed class UndoRedoStack<T>
{
    private readonly List<T> _undo = new();
    private readonly List<T> _redo = new();
    private readonly int _capacity;
    private readonly object _gate = new();

    /// <summary>初始化栈。</summary>
    /// <param name="capacity">撤销容量上限（默认 30；≤0 视为不设限）。</param>
    public UndoRedoStack(int capacity = 30)
    {
        _capacity = capacity;
    }

    /// <summary>是否可撤销。</summary>
    public bool CanUndo
    {
        get { lock (_gate) { return _undo.Count > 0; } }
    }

    /// <summary>是否可重做。</summary>
    public bool CanRedo
    {
        get { lock (_gate) { return _redo.Count > 0; } }
    }

    /// <summary>撤销深度（供 UI 显示/测试）。</summary>
    public int UndoDepth
    {
        get { lock (_gate) { return _undo.Count; } }
    }

    /// <summary>重做深度。</summary>
    public int RedoDepth
    {
        get { lock (_gate) { return _redo.Count; } }
    }

    /// <summary>记录一次新状态：压入撤销栈并清空重做侧（编辑使重做失效）。</summary>
    /// <param name="snapshot">当前状态快照（调用方须存副本，勿传可变引用再改）。</param>
    public void Push(T snapshot)
    {
        lock (_gate)
        {
            _undo.Add(snapshot);
            if (_capacity > 0 && _undo.Count > _capacity)
            {
                _undo.RemoveAt(0);
            }

            _redo.Clear();
        }
    }

    /// <summary>撤销一次：返回上一个状态。</summary>
    /// <returns>可撤销时返回上一态；否则返回 null（default）。</returns>
    public T? Undo()
    {
        lock (_gate)
        {
            if (_undo.Count == 0)
            {
                return default;
            }

            var current = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(current);
            return _undo.Count > 0 ? _undo[^1] : default;
        }
    }

    /// <summary>重做一次：返回下一个状态。</summary>
    /// <returns>可重做时返回重做态；否则返回 null（default）。</returns>
    public T? Redo()
    {
        lock (_gate)
        {
            if (_redo.Count == 0)
            {
                return default;
            }

            var next = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(next);
            return next;
        }
    }

    /// <summary>清空（保存/切换对象时调用）。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
