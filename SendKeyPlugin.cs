using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SendKeyPlugin
{
    [ComVisible(true)]
    [Guid("3F2A7C8E-9B4D-4E5F-A1B2-C3D4E5F6A7B8")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class SendKeyPlugin
    {
        // ───── Win32 SendInput ─────
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;
        const ushort VK_RETURN = 0x0D;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        // ───── 内部工具 ─────
        static INPUT MakeKey(ushort vk, ushort scan, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = scan,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }

        static void SendVKey(ushort vk)
        {
            var inputs = new INPUT[]
            {
                MakeKey(vk, 0, 0),
                MakeKey(vk, 0, KEYEVENTF_KEYUP)
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        static void SendUnicodeChar(char c)
        {
            var inputs = new INPUT[]
            {
                MakeKey(0, c, KEYEVENTF_UNICODE),
                MakeKey(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP)
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // ───── Overwolf JS 调用入口 ─────

        // 异步发送给当前前景窗口（典型用法）
        public void sendChat(string text, object callback)
        {
            var cb = callback as Func<object, object>;

            Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        cb?.Invoke(new { success = false, error = "empty text" });
                        return;
                    }

                    // 1. Enter 打开聊天框
                    SendVKey(VK_RETURN);
                    Thread.Sleep(40);

                    // 2. 逐字符 Unicode 注入
                    foreach (char c in text)
                    {
                        SendUnicodeChar(c);
                        Thread.Sleep(1); // 防止极快连发导致丢字
                    }

                    Thread.Sleep(30);

                    // 3. Enter 发送
                    SendVKey(VK_RETURN);

                    cb?.Invoke(new { success = true });
                }
                catch (Exception ex)
                {
                    cb?.Invoke(new { success = false, error = ex.Message });
                }
            });
        }

        // 探活，便于 JS 端确认插件已加载
        public string ping()
        {
            return "SendKeyPlugin OK";
        }

        // 返回当前前景窗口标题（调试用，可以删）
        public string getForegroundTitle()
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(GetForegroundWindow(), sb, sb.Capacity);
            return sb.ToString();
        }
    }
}