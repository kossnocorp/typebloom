using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Input.Preview.Injection;
using System.Linq;
using System.Text;

namespace TypeBloom
{
    internal class Keyboard
    {
        public struct Chord
        {
            public string to;
            public string from;
        }

        public enum Modifier
        {
            Shift,
            Ctrl,
            Alt,
            Win
        }

        public struct KeyPress
        {
            public VirtualKey key;
            public HashSet<Modifier> modifiers;
        }

        #region DLL imports
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId
        );

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam
        );

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern int ToUnicodeEx(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)] StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags,
            IntPtr dwhkl
        );

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        #endregion

        #region Constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYUP = 0x0101;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_ALT = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        #endregion

        Modifier[] supportedModifiers = { Modifier.Shift };

        VirtualKey[] stopKeys =
        {
            VirtualKey.Enter,
            VirtualKey.Tab,
            VirtualKey.Up,
            VirtualKey.Down,
            VirtualKey.Left,
            VirtualKey.Right,
            VirtualKey.Escape
        };

        private LowLevelKeyboardProc keyboardProc;
        private IntPtr keyboardHookId = IntPtr.Zero;

        string typedText = "";

        static public List<Chord> chords = new List<Chord>
        {
            new Chord { from = "k@", to = "koss@nocorp.me" },
            new Chord { from = "sdc@", to = "sasha@daisychainai.com" },
            new Chord { from = "--\\", to = "—", }
        };

        KeyPress undoShortcut = new KeyPress
        {
            key = VirtualKey.Z,
            modifiers = new HashSet<Modifier> { Modifier.Ctrl }
        };

        private bool expanding = false;

        public Keyboard()
        {
            keyboardProc = HookCallback;
            keyboardHookId = SetHook(keyboardProc);
        }

        ~Keyboard()
        {
            UnhookWindowsHookEx(keyboardHookId);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    proc,
                    GetModuleHandle(curModule.ModuleName),
                    0
                );
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var Next = () =>
            {
                return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
            };

            if (!expanding && nCode >= 0 && wParam == (IntPtr)WM_KEYUP)
            {
                int code = Marshal.ReadInt32(lParam);
                VirtualKey key = (VirtualKey)code;

                var modifiers = new HashSet<Modifier>();

                if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
                    modifiers.Add(Modifier.Shift);
                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
                    modifiers.Add(Modifier.Ctrl);
                if ((GetAsyncKeyState(VK_ALT) & 0x8000) != 0)
                    modifiers.Add(Modifier.Alt);
                if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0)
                    modifiers.Add(Modifier.Win);
                if ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0)
                    modifiers.Add(Modifier.Win);

                var keyPress = new KeyPress { key = key, modifiers = modifiers };

                // Ignore single modifier keys
                if (
                    key == VirtualKey.Shift
                    || key == VirtualKey.Control
                    || key == VirtualKey.Menu // Alt
                    || key == VirtualKey.LeftWindows
                    || key == VirtualKey.RightWindows
                )
                    return Next();

                // Process delete
                if (key == VirtualKey.Back)
                {
                    if (modifiers.Contains(Modifier.Ctrl) || modifiers.Contains(Modifier.Alt))
                        Reset();
                    else if (typedText.Length > 0)
                        typedText = typedText.Substring(0, typedText.Length - 1);

                    return Next();
                }

                // Process undo
                var undoPressed =
                    undoShortcut.key == key && undoShortcut.modifiers.SetEquals(modifiers);
                if (undoPressed)
                {
                    Debug.WriteLine("TODO: Process undo");

                    Reset();
                    return Next();
                }

                // If any of the stop keys is pressed, reset the state
                var nonSupportedModifiers = new HashSet<Modifier>(modifiers);
                nonSupportedModifiers.ExceptWith(supportedModifiers);
                if (stopKeys.Contains(key) || nonSupportedModifiers.Count > 0)
                {
                    Reset();
                    return Next();
                }

                typedText += PressToString(keyPress);

                Update();
            }

            return Next();
        }

        private void Update()
        {
            var index = chords.FindIndex(chord => typedText.EndsWith(chord.from));

            if (index != -1)
            {
                var toExpand = chords[index];
                Expand(toExpand);
            }
        }

        async private void Expand(Chord chord)
        {
            expanding = true;
            var injector = InputInjector.TryCreate();

            for (int i = 0; i < chord.from.Length; i++)
            {
                injector.InjectKeyboardInput(
                    new[]
                    {
                        new InjectedInputKeyboardInfo { VirtualKey = (ushort)VirtualKey.Back, }
                    }
                );
                await Task.Delay(10);
            }

            foreach (char c in chord.to)
            {
                injector.InjectKeyboardInput(
                    new[]
                    {
                        new InjectedInputKeyboardInfo
                        {
                            ScanCode = c,
                            KeyOptions = InjectedInputKeyOptions.Unicode
                        },
                    }
                );
                await Task.Delay(10);

                injector.InjectKeyboardInput(
                    new[]
                    {
                        new InjectedInputKeyboardInfo
                        {
                            ScanCode = c,
                            KeyOptions =
                                InjectedInputKeyOptions.Unicode | InjectedInputKeyOptions.KeyUp
                        }
                    }
                );
                await Task.Delay(10);
            }

            expanding = false;

            Reset();
        }

        private void Reset()
        {
            typedText = "";
        }

        private string PressToString(KeyPress keyPress)
        {
            byte[] keyState = new byte[256];

            foreach (var modifier in keyPress.modifiers)
            {
                switch (modifier)
                {
                    case Modifier.Shift:
                        keyState[VK_SHIFT] = 0xff;
                        break;
                    case Modifier.Ctrl:
                        keyState[VK_CONTROL] = 0xff;
                        break;
                    case Modifier.Alt:
                        keyState[VK_ALT] = 0xff;
                        break;
                    case Modifier.Win:
                        keyState[VK_LWIN] = 0xff;
                        keyState[VK_RWIN] = 0xff;
                        break;
                }
            }

            StringBuilder buffer = new StringBuilder(10);
            IntPtr layout = GetLayout();

            uint virtualKeyCode = (uint)keyPress.key;
            ToUnicodeEx(virtualKeyCode, 0, keyState, buffer, buffer.Capacity, 0, layout);

            return buffer.ToString();
        }

        private IntPtr GetLayout()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint threadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);
            return GetKeyboardLayout(threadId);
        }
    }
}
