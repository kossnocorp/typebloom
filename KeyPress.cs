using System;
using System.Collections.Generic;
using System.Text;
using Windows.System;
using System.Runtime.InteropServices;

namespace TypeBloom
{
    internal struct KeyPress
    {
        public int code;

        public VirtualKey key;

        public HashSet<KeyModifier> modifiers;

        public bool Equals(KeyPress press)
        {
            return key == press.key && modifiers.SetEquals(press.modifiers);
        }

        public string ToUnicode()
        {
            byte[] keyState = new byte[256];

            foreach (var modifier in modifiers)
            {
                switch (modifier)
                {
                    case KeyModifier.Shift:
                        keyState[(int)VirtualKey.Shift] = 0xff;
                        break;
                    case KeyModifier.Ctrl:
                        keyState[(int)VirtualKey.Control] = 0xff;
                        break;
                    case KeyModifier.Alt:
                        keyState[(int)VirtualKey.Menu] = 0xff;
                        break;
                    case KeyModifier.Win:
                        keyState[(int)VirtualKey.LeftWindows] = 0xff;
                        keyState[(int)VirtualKey.RightWindows] = 0xff;
                        break;
                }
            }

            StringBuilder buffer = new StringBuilder(10);
            IntPtr layout = Keyboard.GetLayout();

            uint virtualKeyCode = (uint)key;
            ToUnicodeEx(virtualKeyCode, 0, keyState, buffer, buffer.Capacity, 0, layout);

            return buffer.ToString();
        }

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

        public static KeyPress FromCode(int code)
        {
            VirtualKey key = (VirtualKey)code;

            var modifiers = new HashSet<KeyModifier>();
            if ((GetAsyncKeyState((int)VirtualKey.Shift) & 0x8000) != 0)
                modifiers.Add(KeyModifier.Shift);
            if ((GetAsyncKeyState((int)VirtualKey.LeftShift) & 0x8000) != 0)
                modifiers.Add(KeyModifier.Shift);
            if ((GetAsyncKeyState((int)VirtualKey.RightShift) & 0x8000) != 0)
                modifiers.Add(KeyModifier.Shift);
            if ((GetAsyncKeyState((int)VirtualKey.Control) & 0x8000) != 0)
                modifiers.Add(KeyModifier.Ctrl);
            if ((GetAsyncKeyState((int)VirtualKey.Menu) & 0x8000) != 0)
                modifiers.Add(KeyModifier.Alt);
            if ((GetAsyncKeyState((int)VirtualKey.LeftWindows) & 0x8000) != 0)
                modifiers.Add(KeyModifier.Win);
            if ((GetAsyncKeyState((int)VirtualKey.RightWindows) & 0x8000) != 0)
                modifiers.Add(KeyModifier.Win);

            var keyPress = new KeyPress
            {
                code = code,
                key = key,
                modifiers = modifiers
            };
            return keyPress;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
    }
}
