namespace Object1688.Shared.Input;

/// <summary>
/// 全局热键定义（架构 §8.7 / F-12）：修饰键位掩码 + Win32 虚拟键码。
/// <see cref="Modifiers"/> 取值与 Win32 <c>RegisterHotKey</c> 的 fsModifiers 一致。
/// </summary>
public readonly record struct HotkeySpec(uint Modifiers, uint VirtualKey, string Normalized)
{
    /// <summary>Alt 修饰键（MOD_ALT）。</summary>
    public const uint ModAlt = 0x0001;

    /// <summary>Ctrl 修饰键（MOD_CONTROL）。</summary>
    public const uint ModControl = 0x0002;

    /// <summary>Shift 修饰键（MOD_SHIFT）。</summary>
    public const uint ModShift = 0x0004;

    /// <summary>Win 修饰键（MOD_WIN）。</summary>
    public const uint ModWin = 0x0008;
}

/// <summary>
/// 热键字符串解析（F-12/F-20）：将 <c>"Ctrl+Alt+O"</c> 形式解析为 <see cref="HotkeySpec"/>。
/// 支持修饰键 Ctrl/Alt/Shift/Win + 主键 A-Z / 0-9 / F1-F12 / 若干命名键；至少一个修饰键与一个主键。
/// </summary>
public static class HotkeyParser
{
    /// <summary>
    /// 解析热键字符串。
    /// </summary>
    /// <param name="text">如 <c>Ctrl+Alt+O</c>；空/非法返回 false。</param>
    /// <param name="spec">解析结果。</param>
    public static bool TryParse(string? text, out HotkeySpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        uint modifiers = 0;
        uint virtualKey = 0;
        var keySeen = false;
        var keyToken = string.Empty;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeySpec.ModControl;
                    break;
                case "alt":
                    modifiers |= HotkeySpec.ModAlt;
                    break;
                case "shift":
                    modifiers |= HotkeySpec.ModShift;
                    break;
                case "win":
                case "windows":
                    modifiers |= HotkeySpec.ModWin;
                    break;
                default:
                    if (keySeen || !TryParseKey(part, out virtualKey))
                    {
                        return false;
                    }

                    keySeen = true;
                    keyToken = part.ToUpperInvariant();
                    break;
            }
        }

        if (!keySeen || modifiers == 0)
        {
            return false;
        }

        spec = new HotkeySpec(modifiers, virtualKey, $"{BuildModifiers(modifiers)}+{keyToken}");
        return true;
    }

    private static string BuildModifiers(uint modifiers)
    {
        var parts = new List<string>(4);
        if ((modifiers & HotkeySpec.ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & HotkeySpec.ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & HotkeySpec.ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & HotkeySpec.ModWin) != 0)
        {
            parts.Add("Win");
        }

        return string.Join('+', parts);
    }

    private static bool TryParseKey(string token, out uint virtualKey)
    {
        virtualKey = 0;
        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                virtualKey = c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }

            return false;
        }

        if ((token[0] is 'F' or 'f') && int.TryParse(token.AsSpan(1), out var fn) && fn is >= 1 and <= 12)
        {
            virtualKey = (uint)(0x70 + fn - 1);
            return true;
        }

        virtualKey = token.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "enter" or "return" => 0x0D,
            "esc" or "escape" => 0x1B,
            "tab" => 0x09,
            "backspace" => 0x08,
            "insert" => 0x2D,
            "delete" or "del" => 0x2E,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            _ => 0,
        };

        return virtualKey != 0;
    }
}
