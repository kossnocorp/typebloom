using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Input.Preview.Injection;
using System.Linq;

namespace TypeBloom
{
    internal class Controller
    {
        static int keyboardDelay = 10;

        public struct Snippet
        {
            public string to;
            public string from;
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
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        #endregion

        #region Constants
        private const int WH_KEYBOARD_LL = 13;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;
        #endregion

        KeyModifier[] supportedModifiers = { KeyModifier.Shift };

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

        static public List<Snippet> snippets = new List<Snippet>
        {
            new Snippet { from = "k@", to = "koss@nocorp.me" },
            new Snippet { from = "sdc@", to = "sasha@daisychainai.com" },
            new Snippet { from = "--\\", to = "—", },
            new Snippet { from = "->\\", to = "→", },
            new Snippet { from = "<-\\", to = "←", },
            new Snippet { from = "-v\\", to = "↓", },
            new Snippet { from = "-^\\", to = "↑", },
            new Snippet { from = "v\\\\", to = "✔" },
            new Snippet { from = "x\\\\", to = "❌" },
            new Snippet { from = "...\\", to = "…" },
        };

        KeyPress undoShortcut = new KeyPress
        {
            key = VirtualKey.Z,
            modifiers = new HashSet<KeyModifier> { KeyModifier.Ctrl }
        };

        KeyPress translateShortcut = new KeyPress
        {
            key = VirtualKey.Z,
            modifiers = new HashSet<KeyModifier> { KeyModifier.Alt, KeyModifier.Shift }
        };

        private Dictionary<int, KeyPress> keyPresses = new Dictionary<int, KeyPress>();

        private bool expanding = false;

        private KeyboardBuffer buffer = new KeyboardBuffer();

        public Controller()
        {
            keyboardProc = HookCallback;
            keyboardHookId = SetHook(keyboardProc);
        }

        ~Controller()
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

            if (nCode >= 0)
            {
                var code = Marshal.ReadInt32(lParam);

                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    var keyPress = KeyPress.FromCode(code);
                    if (!keyPresses.ContainsKey(code))
                        keyPresses.Add(keyPress.code, keyPress);
                }
                else if (
                    (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                    && keyPresses.ContainsKey(code)
                )
                {
                    Debug.WriteLine("Key up: " + code);

                    var keyPress = keyPresses[code];
                    keyPresses.Remove(code);

                    Debug.WriteLine(
                        keyPress.code
                            + " "
                            + "shift: "
                            + keyPress.modifiers.Contains(KeyModifier.Shift)
                    );

                    if (!expanding && !IsModifierKeyPress(keyPress))
                    {
                        // Process delete
                        if (keyPress.key == VirtualKey.Back)
                        {
                            if (
                                keyPress.modifiers.Contains(KeyModifier.Ctrl)
                                || keyPress.modifiers.Contains(KeyModifier.Alt)
                            )
                                buffer.Reset();
                            else
                                buffer.Delete();

                            return Next();
                        }

                        // Process undo
                        if (keyPress.Equals(undoShortcut))
                        {
                            Debug.WriteLine("TODO: Process undo");

                            buffer.Reset();
                            return Next();
                        }

                        // Process translate
                        if (keyPress.Equals(translateShortcut))
                        {
                            Debug.WriteLine("Translating...");
                            Translate();
                            return Next();
                        }

                        // If any of the stop keys is pressed, reset the state
                        var nonSupportedModifiers = new HashSet<KeyModifier>(keyPress.modifiers);
                        nonSupportedModifiers.ExceptWith(supportedModifiers);
                        if (stopKeys.Contains(keyPress.key) || nonSupportedModifiers.Count > 0)
                        {
                            buffer.Reset();
                            return Next();
                        }

                        var typed = buffer.Add(keyPress);
                        Debug.WriteLine(
                            "Typed `" + typed + "`, new text buffer: \"" + buffer.text + "\""
                        );

                        Update();
                    }
                }
            }

            return Next();
        }

        private void Update()
        {
            var index = snippets.FindIndex(snippet => buffer.text.EndsWith(snippet.from));

            if (index != -1)
            {
                var toExpand = snippets[index];
                Debug.WriteLine(
                    "Expanding snippet from \"" + toExpand.from + "\" to \"" + toExpand.to + "\""
                );
                Expand(toExpand);
            }
        }

        async private void Expand(Snippet snippet)
        {
            expanding = true;

            var injector = InputInjector.TryCreate();

            for (int i = 0; i < snippet.from.Length; i++)
            {
                injector.InjectKeyboardInput(
                    new[]
                    {
                        new InjectedInputKeyboardInfo { VirtualKey = (ushort)VirtualKey.Back, }
                    }
                );
                await Task.Delay(keyboardDelay);
            }

            foreach (char c in snippet.to)
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
                await Task.Delay(keyboardDelay);

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
                await Task.Delay(keyboardDelay);
            }

            expanding = false;

            buffer.Reset();
        }

        async private void Translate()
        {
            expanding = true;

            var injector = InputInjector.TryCreate();

            var spaceIndex = buffer.text.LastIndexOf(" ");
            var wordIndex = spaceIndex + 1;
            //if (spaceIndex == -1)
            //    spaceIndex = 0;
            Debug.WriteLine("Space index: " + spaceIndex);
            var wordLength = buffer.text.Length - wordIndex;
            Debug.WriteLine("Word length: " + wordLength);
            Debug.WriteLine("Buffer size: " + buffer.text.Length);
            Debug.WriteLine("Presses count: " + buffer.keyPresses.Count);
            Debug.WriteLine("Range from: " + (buffer.keyPresses.Count - wordLength - 1));
            var keyPresses = buffer.keyPresses.GetRange(wordIndex, wordLength);

            // First delete the word

            for (int i = 0; i < wordLength; i++)
            {
                Debug.WriteLine("--- Sending delete");
                injector.InjectKeyboardInput(
                    new[]
                    {
                        new InjectedInputKeyboardInfo { VirtualKey = (ushort)VirtualKey.Back, }
                    }
                );
                await Task.Delay(keyboardDelay);
            }

            // Now switch the language

            injector.InjectKeyboardInput(
                new[]
                {
                    new InjectedInputKeyboardInfo
                    {
                        VirtualKey = (ushort)VirtualKey.Menu,
                        KeyOptions = InjectedInputKeyOptions.None
                    },
                    new InjectedInputKeyboardInfo
                    {
                        VirtualKey = (ushort)VirtualKey.Shift,
                        KeyOptions = InjectedInputKeyOptions.None
                    },
                }
            );
            await Task.Delay(keyboardDelay);

            injector.InjectKeyboardInput(
                new[]
                {
                    new InjectedInputKeyboardInfo
                    {
                        VirtualKey = (ushort)VirtualKey.Menu,
                        KeyOptions = InjectedInputKeyOptions.KeyUp
                    },
                    new InjectedInputKeyboardInfo
                    {
                        VirtualKey = (ushort)VirtualKey.Shift,
                        KeyOptions = InjectedInputKeyOptions.KeyUp
                    },
                }
            );
            await Task.Delay(keyboardDelay);

            // Now type the word in the new language

            var newWord = string.Join("", keyPresses.Select(keyPress => keyPress.ToUnicode()));

            foreach (char c in newWord)
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
                await Task.Delay(keyboardDelay);

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
                await Task.Delay(keyboardDelay);
            }

            buffer.Reset();

            expanding = false;
        }

        private bool IsModifierKeyPress(KeyPress keyPress)
        {
            return keyPress.key == VirtualKey.Shift
                || keyPress.key == VirtualKey.LeftShift
                || keyPress.key == VirtualKey.RightShift
                || keyPress.key == VirtualKey.Control
                || keyPress.key == VirtualKey.Menu // Alt
                || keyPress.key == VirtualKey.LeftWindows
                || keyPress.key == VirtualKey.RightWindows;
        }
    }
}
