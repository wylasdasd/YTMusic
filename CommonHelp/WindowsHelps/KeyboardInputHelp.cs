using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace CommonHelp;

/// <summary>
/// 功能职责：Windows 键盘输入模拟。基于 Win32 SendInput 以 Unicode 直发键入文本。仅支持 Windows。不负责跨平台输入。键入前会取消前台 IME 组词并暂时分离输入法，松开仍按下的修饰键。
/// 方法群组：键入单段、依次键入多段。
/// </summary>
[SupportedOSPlatform("windows")]
public static class KeyboardInputHelp
{
    private const uint InputKeyboard = 1;
    private const uint KeyeventfExtendedkey = 0x0001;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfUnicode = 0x0004;
    private const ushort VkReturn = 0x0D;
    private const ushort VkTab = 0x09;
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkMenu = 0x12;
    private const ushort VkLwin = 0x5B;
    private const ushort VkRwin = 0x5C;
    private const ushort VkLshift = 0xA0;
    private const ushort VkRshift = 0xA1;
    private const ushort VkLcontrol = 0xA2;
    private const ushort VkRcontrol = 0xA3;
    private const ushort VkLmenu = 0xA4;
    private const ushort VkRmenu = 0xA5;
    private const uint MapvkVkToVsc = 0;
    private const uint NiCompositionstr = 0x0015;
    private const uint CpsCancel = 0x0004;
    private const int SendChunkEvents = 32;
    private const int SendRetryDelayMs = 15;
    private const int SendRetryCount = 3;

    /// <summary>
    /// 功能作用：空串直接返回；延时后键入。\n → 回车、\t → Tab、\r 忽略。charIntervalMs &lt;= 0 时整段连发（分块）。
    /// </summary>
    /// <param name="text">要键入的文本。</param>
    /// <param name="startDelayMs">开始前等待毫秒，默认 3000。</param>
    /// <param name="charIntervalMs">字符间隔毫秒；&lt;= 0 时连发。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>无返回值；向当前前台窗口发送按键。</returns>
    /// <remarks>异步：可等待。含起始延时与字符间隔延时。非 Windows 抛 PlatformNotSupportedException。</remarks>
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

        using var ime = ImeInputScope.TryBegin();
        ReleaseHeldModifiers();
        await TypeNormalizedAsync(text, Math.Max(0, charIntervalMs), cancellationToken);
    }

    /// <summary>
    /// 功能作用：依次键入多段。仅当首项非空时等待起始延时；项间停顿；默认每项后补回车。整批共用一次 IME 分离。
    /// </summary>
    /// <param name="items">多段文本。</param>
    /// <param name="startDelayMs">首项前等待毫秒。</param>
    /// <param name="charIntervalMs">字符间隔毫秒。</param>
    /// <param name="itemIntervalMs">项间停顿毫秒。</param>
    /// <param name="pressEnterAfterEach">每项后是否补回车。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>无返回值；向当前前台窗口发送按键。</returns>
    /// <remarks>异步：可等待。含起始延时、字符间隔与项间延时。</remarks>
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

        if (delay > 0 && !string.IsNullOrEmpty(items[0]))
        {
            await Task.Delay(delay, cancellationToken);
        }

        using var ime = ImeInputScope.TryBegin();
        ReleaseHeldModifiers();

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i] ?? string.Empty;
            await TypeNormalizedAsync(item, charDelay, cancellationToken);

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

    private static async Task TypeNormalizedAsync(string text, int charIntervalMs, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var normalized = text.Replace("\r\n", "\n");
        if (charIntervalMs <= 0)
        {
            SendInputChunks(BuildInputs(normalized));
            return;
        }

        foreach (var rune in normalized.EnumerateRunes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rune.Value == '\r')
            {
                continue;
            }

            SendInputChunks(BuildRuneInputs(rune));
            await Task.Delay(charIntervalMs, cancellationToken);
        }
    }

    private static Input[] BuildInputs(string normalized)
    {
        var list = new List<Input>(normalized.Length * 2);
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (rune.Value == '\r')
            {
                continue;
            }

            AppendRuneInputs(list, rune);
        }

        return [.. list];
    }

    private static Input[] BuildRuneInputs(Rune rune)
    {
        var list = new List<Input>(4);
        AppendRuneInputs(list, rune);
        return [.. list];
    }

    private static void AppendRuneInputs(List<Input> list, Rune rune)
    {
        if (rune.Value == '\n')
        {
            AppendVirtualKey(list, VkReturn);
            return;
        }

        if (rune.Value == '\t')
        {
            AppendVirtualKey(list, VkTab);
            return;
        }

        Span<char> utf16Buffer = stackalloc char[2];
        var units = rune.EncodeToUtf16(utf16Buffer);

        // 代理对先全部 keydown 再全部 keyup，避免半个字符被目标窗口丢掉。
        for (var i = 0; i < units; i++)
        {
            list.Add(CreateUnicodeInput(utf16Buffer[i], keyUp: false));
        }

        for (var i = 0; i < units; i++)
        {
            list.Add(CreateUnicodeInput(utf16Buffer[i], keyUp: true));
        }
    }

    private static void AppendVirtualKey(List<Input> list, ushort key)
    {
        list.Add(CreateVirtualKeyInput(key, keyUp: false));
        list.Add(CreateVirtualKeyInput(key, keyUp: true));
    }

    private static void SendVirtualKey(ushort key)
    {
        SendInputChunks([CreateVirtualKeyInput(key, keyUp: false), CreateVirtualKeyInput(key, keyUp: true)]);
    }

    private static Input CreateUnicodeInput(char unit, bool keyUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                KeyboardInput = new KeybdInput
                {
                    WScan = unit,
                    DwFlags = keyUp ? KeyeventfUnicode | KeyeventfKeyup : KeyeventfUnicode
                }
            }
        };
    }

    private static Input CreateVirtualKeyInput(ushort key, bool keyUp, bool extended = false)
    {
        uint flags = 0;
        if (extended)
        {
            flags |= KeyeventfExtendedkey;
        }

        if (keyUp)
        {
            flags |= KeyeventfKeyup;
        }

        return new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                KeyboardInput = new KeybdInput
                {
                    WVk = key,
                    WScan = (ushort)MapVirtualKey(key, MapvkVkToVsc),
                    DwFlags = flags
                }
            }
        };
    }

    private static void ReleaseHeldModifiers()
    {
        ReleaseIfDown(VkRmenu, extended: true);
        ReleaseIfDown(VkLmenu);
        ReleaseIfDown(VkMenu);
        ReleaseIfDown(VkRcontrol, extended: true);
        ReleaseIfDown(VkLcontrol);
        ReleaseIfDown(VkControl);
        ReleaseIfDown(VkRshift);
        ReleaseIfDown(VkLshift);
        ReleaseIfDown(VkShift);
        ReleaseIfDown(VkRwin, extended: true);
        ReleaseIfDown(VkLwin, extended: true);
    }

    private static void ReleaseIfDown(ushort key, bool extended = false)
    {
        if ((GetAsyncKeyState(key) & 0x8000) == 0)
        {
            return;
        }

        SendInputChunks([CreateVirtualKeyInput(key, keyUp: true, extended)]);
    }

    private static void SendInputChunks(Input[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        var offset = 0;
        while (offset < inputs.Length)
        {
            var count = Math.Min(SendChunkEvents, inputs.Length - offset);
            var slice = count == inputs.Length
                ? inputs
                : inputs.AsSpan(offset, count).ToArray();
            SendInputsWithRetry(slice);
            offset += count;
        }
    }

    private static void SendInputsWithRetry(Input[] inputs)
    {
        var remaining = inputs;
        for (var attempt = 0; attempt < SendRetryCount; attempt++)
        {
            var sent = SendInputsNative(remaining);
            if (sent == remaining.Length)
            {
                return;
            }

            if (sent > 0 && sent < remaining.Length)
            {
                var next = new Input[remaining.Length - sent];
                Array.Copy(remaining, (int)sent, next, 0, next.Length);
                remaining = next;
            }

            if (attempt < SendRetryCount - 1)
            {
                Thread.Sleep(SendRetryDelayMs);
            }
        }

        var error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException($"SendInput failed. Win32Error={error}.");
    }

    private static uint SendInputsNative(Input[] inputs)
    {
        var size = Marshal.SizeOf<Input>();
        var ptr = Marshal.AllocHGlobal(size * inputs.Length);
        try
        {
            for (var i = 0; i < inputs.Length; i++)
            {
                Marshal.StructureToPtr(inputs[i], ptr + (i * size), false);
            }

            return SendInput((uint)inputs.Length, ptr, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private sealed class ImeInputScope : IDisposable
    {
        private readonly IntPtr _hwnd;
        private readonly IntPtr _previousImc;
        private bool _disposed;

        private ImeInputScope(IntPtr hwnd, IntPtr previousImc)
        {
            _hwnd = hwnd;
            _previousImc = previousImc;
        }

        public static ImeInputScope? TryBegin()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    return null;
                }

                var himc = ImmGetContext(hwnd);
                if (himc != IntPtr.Zero)
                {
                    ImmNotifyIME(himc, NiCompositionstr, CpsCancel, 0);
                    ImmSetOpenStatus(himc, false);
                    ImmReleaseContext(hwnd, himc);
                }

                var previous = ImmAssociateContext(hwnd, IntPtr.Zero);
                var associated = ImmGetContext(hwnd);
                if (associated != IntPtr.Zero)
                {
                    ImmReleaseContext(hwnd, associated);
                    return null;
                }

                return new ImeInputScope(hwnd, previous);
            }
            catch (DllNotFoundException)
            {
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (_hwnd != IntPtr.Zero)
                {
                    ImmAssociateContext(_hwnd, _previousImc);
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, IntPtr pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hImc);

    [DllImport("imm32.dll")]
    private static extern bool ImmSetOpenStatus(IntPtr hImc, bool fOpen);

    [DllImport("imm32.dll")]
    private static extern bool ImmNotifyIME(IntPtr hImc, uint dwAction, uint dwIndex, uint dwValue);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmAssociateContext(IntPtr hWnd, IntPtr hImc);

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
