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
        // ---------- SendInput 结构 ----------
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
        const int SW_RESTORE = 9;

        // ---------- Win32 imports ----------
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // ---------- 输入辅助 ----------
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

        // ---------- 窗口查找 ----------
        static string GetWindowTitle(IntPtr hWnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        static string GetWindowClassName(IntPtr hWnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // 找 LoL 游戏窗口（不是大厅，不是 overlay）
        static IntPtr FindLeagueGameWindow()
        {
            // 优先按已知类名找
            IntPtr hwnd = FindWindow("RiotWindowClass", null);
            if (hwnd != IntPtr.Zero) return hwnd;

            // 兜底：按标题找
            hwnd = FindWindow(null, "League of Legends (TM) Client");
            if (hwnd != IntPtr.Zero) return hwnd;

            // 再兜底：枚举所有可见窗口，匹配标题包含 League of Legends 且不是 Overwolf 的
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h)) return true;
                string title = GetWindowTitle(h);
                string cls = GetWindowClassName(h);
                if (string.IsNullOrEmpty(title)) return true;

                if (title.IndexOf("League of Legends", StringComparison.OrdinalIgnoreCase) >= 0
                    && cls.IndexOf("Overwolf", StringComparison.OrdinalIgnoreCase) < 0
                    && title.IndexOf("Overwolf", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    found = h;
                    return false; // 停止枚举
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        static bool FocusWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

            // 用 AttachThreadInput 套路绕过 SetForegroundWindow 的限制
            IntPtr fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, out _);
            uint myThread = GetCurrentThreadId();

            bool attached = false;
            if (fgThread != 0 && fgThread != myThread)
                attached = AttachThreadInput(myThread, fgThread, true);

            bool ok = SetForegroundWindow(hwnd);

            if (attached)
                AttachThreadInput(myThread, fgThread, false);

            return ok;
        }

        // ---------- 返回结果辅助 ----------
        static Dictionary<string, object> Result(bool success, string error = null)
        {
            var d = new Dictionary<string, object>();
            d["success"] = success;
            if (error != null) d["error"] = error;
            return d;
        }

        // ---------- 公开 API ----------
        public void sendChat(string text, Action<object> callback)
        {
            Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        if (callback != null) callback(Result(false, "empty text"));
                        return;
                    }

                    IntPtr hwnd = FindLeagueGameWindow();
                    if (hwnd == IntPtr.Zero)
                    {
                        if (callback != null) callback(Result(false, "LoL game window not found"));
                        return;
                    }

                    FocusWindow(hwnd);

                    // 等焦点切换稳定
                    Thread.Sleep(150);

                    // 打开聊天框
                    SendVKey(VK_RETURN);
                    Thread.Sleep(60);

                    // 输入字符
                    foreach (char c in text)
                    {
                        SendUnicodeChar(c);
                        Thread.Sleep(2);
                    }

                    Thread.Sleep(40);

                    // 发送
                    SendVKey(VK_RETURN);

                    if (callback != null) callback(Result(true));
                }
                catch (Exception ex)
                {
                    if (callback != null) callback(Result(false, ex.Message));
                }
            });
        }

        public void ping(Action<object> callback)
        {
            if (callback != null) callback("SendKeyPlugin OK");
        }

        public void getForegroundTitle(Action<object> callback)
        {
            IntPtr hwnd = GetForegroundWindow();
            string title = GetWindowTitle(hwnd);
            string cls = GetWindowClassName(hwnd);
            if (callback != null) callback(title + " [" + cls + "]");
        }

        public void findLeagueWindow(Action<object> callback)
        {
            IntPtr hwnd = FindLeagueGameWindow();
            if (hwnd == IntPtr.Zero)
            {
                if (callback != null) callback(Result(false, "not found"));
                return;
            }
            var d = new Dictionary<string, object>();
            d["success"] = true;
            d["title"] = GetWindowTitle(hwnd);
            d["className"] = GetWindowClassName(hwnd);
            d["hwnd"] = hwnd.ToInt64();
            if (callback != null) callback(d);
        }
    }
}
