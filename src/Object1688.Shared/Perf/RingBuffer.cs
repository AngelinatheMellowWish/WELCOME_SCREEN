namespace Object1688.Shared.Perf;

/// <summary>
/// 有界环形缓冲（架构 §6.3：性能窗口时间窗/事件流环形保留）。
/// 线程安全；容量固定，超出覆盖最旧。枚举按时间序（旧→新）。
/// </summary>
/// <typeparam name="T">元素类型。</typeparam>
public sealed class RingBuffer<T>
{
    private readonly T[] _items;
    private readonly object _gate = new();
    private int _head;      // 下一个写入槽
    private int _count;     // 当前有效元素数

    /// <summary>初始化环形缓冲。</summary>
    /// <param name="capacity">容量上限（必须 ≥ 1）。</param>
    public RingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _items = new T[capacity];
    }

    /// <summary>当前有效元素数。</summary>
    public int Count
    {
        get { lock (_gate) { return _count; } }
    }

    /// <summary>容量上限。</summary>
    public int Capacity => _items.Length;

    /// <summary>追加元素（满则覆盖最旧）。</summary>
    /// <param name="item">元素。</param>
    public void Append(T item)
    {
        lock (_gate)
        {
            _items[_head] = item;
            _head = (_head + 1) % _items.Length;
            if (_count < _items.Length)
            {
                _count++;
            }
        }
    }

    /// <summary>按时间序（旧→新）返回全部元素快照。</summary>
    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate)
        {
            var result = new T[_count];
            var start = (_head - _count + _items.Length) % _items.Length;
            for (var i = 0; i < _count; i++)
            {
                result[i] = _items[(start + i) % _items.Length];
            }

            return result;
        }
    }

    /// <summary>清空缓冲。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_items);
            _head = 0;
            _count = 0;
        }
    }
}
