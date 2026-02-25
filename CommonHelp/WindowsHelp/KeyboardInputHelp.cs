using System.Runtime.InteropServices;
using System.Text;

namespace CommonHelp.WindowsHelp;

public static class KeyboardInputHelp
{
    private const uint InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;
    private const ushort VkReturn = 0x0D;
    private const ushort VkTab = 0x09;

    public static async Task TypeTextAsync(string text, int startDelayMs = 3000, int charIntervalMs = 30, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Keyboard simulation is only supported on Windows.");
        }

        if (startDelayMs > 0)
        {
            await Task.Delay(startDelayMs, cancellationToken);
        }

        var normalized = text.Replace("\r\n", "\n");
        foreach (var rune in normalized.EnumerateRunes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rune.Value == '\r')
            {
                continue;
            }

            if (rune.Value == '\n')
            {
                SendVirtualKey(VkReturn);
            }
            else if (rune.Value == '\t')
            {
                SendVirtualKey(VkTab);
            }
            else
            {
                SendUnicodeRune(rune);
            }

            if (charIntervalMs > 0)
            {
                await Task.Delay(charIntervalMs, cancellationToken);
            }
        }
    }

    public static async Task TypeBatchAsync(
        IReadOnlyList<string> items,
        int startDelayMs = 3000,
        int charIntervalMs = 30,
        int itemIntervalMs = 200,
        bool pressEnterAfterEach = true,
        CancellationToken cancellationToken = default)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Keyboard simulation is only supported on Windows.");
        }

        var delay = Math.Max(0, startDelayMs);
        var charDelay = Math.Max(0, charIntervalMs);
        var interval = Math.Max(0, itemIntervalMs);

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i] ?? string.Empty;
            await TypeTextAsync(item, i == 0 ? delay : 0, charDelay, cancellationToken);

            if (pressEnterAfterEach)
            {
                SendVirtualKey(VkReturn);
            }

            if (interval > 0 && i < items.Count - 1)
            {
                await Task.Delay(interval, cancellationToken);
            }
        }
    }

    private static void SendUnicodeRune(Rune rune)
    {
        Span<char> utf16Buffer = stackalloc char[2];
        var units = rune.EncodeToUtf16(utf16Buffer);
        var inputs = new Input[units * 2];

        for (var i = 0; i < units; i++)
        {
            var unit = utf16Buffer[i];
            inputs[i * 2] = new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeybdInput
                    {
                        WScan = unit,
                        DwFlags = KeyeventfUnicode
                    }
                }
            };

            inputs[i * 2 + 1] = new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeybdInput
                    {
                        WScan = unit,
                        DwFlags = KeyeventfUnicode | KeyeventfKeyup
                    }
                }
            };
        }

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SendInput failed for rune '{rune}'. Win32Error={error}.");
        }
    }

    private static void SendVirtualKey(ushort key)
    {
        var inputs = new[]
        {
            new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeybdInput
                    {
                        WVk = key
                    }
                }
            },
            new Input
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    KeyboardInput = new KeybdInput
                    {
                        WVk = key,
                        DwFlags = KeyeventfKeyup
                    }
                }
            }
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SendInput failed for virtual key '{key}'. Win32Error={error}.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput MouseInput;

        [FieldOffset(0)]
        public KeybdInput KeyboardInput;

        [FieldOffset(0)]
        public HardwareInput HardwareInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint UMsg;
        public ushort WParamL;
        public ushort WParamH;
    }
}
