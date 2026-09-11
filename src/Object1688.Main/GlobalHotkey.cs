using System.Runtime.InteropServices;
using System.Windows.Interop;
using Object1688.Shared.Input;

namespace Object1688.Main;

/// <summary>
/// 全局热键注册器（F-12/F-20/AC-71）。
/// 基于 Win32 <c>RegisterHotKey</c> + 一个隐藏的消息窗口（<see cref="HwndSource"/>）接收 WM_HOTKEY；
/// 注册失败（被其他程序占用）返回 false，调用方记 PRC-W-2004 并进入无快捷键降级。
/// 必须在 UI 线程创建/注册（HwndSource 消息循环所在线程）。
/// </summary>
internal sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x1688;
    private const uint ModNoRepeat = 0x4000;

    private HwndSource? _source;

    /// <summary>热键按下（WM_HOTKEY）。</summary>
    public event Action? Pressed;

    /// <summary>注册热键；重复注册会先注销旧热键。</summary>
    /// <returns>成功 true；失败 false（已被占用）。</returns>
    public bool TryRegister(HotkeySpec spec)
    {
        if (_source is null)
        {
            var parameters = new HwndSourceParameters("Object1688.Hotkey")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0, // 隐藏（无 WS_VISIBLE）
            };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);
        }
        else
        {
            _ = UnregisterHotKey(_source.Handle, HotkeyId);
        }

        return RegisterHotKey(_source.Handle, HotkeyId, spec.Modifiers | ModNoRepeat, spec.VirtualKey);
    }

    /// <summary>注销热键（保留消息窗口）。</summary>
    public void Unregister()
    {
        if (_source is not null)
        {
            _ = UnregisterHotKey(_source.Handle, HotkeyId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unregister();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
