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

        static Dictionary<string, object> Result(bool success, string error = null)
        {
            var d = new Dictionary<string, object>();
            d["success"] = success;
            if (error != null) d["error"] = error;
            return d;
        }

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

                    SendVKey(VK_RETURN);
                    Thread.Sleep(40);

                    foreach (char c in text)
                    {
                        SendUnicodeChar(c);
                        Thread.Sleep(1);
                    }

                    Thread.Sleep(30);

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
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(GetForegroundWindow(), sb, sb.Capacity);
            if (callback != null) callback(sb.ToString());
        }
    }
}
