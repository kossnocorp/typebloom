using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeBloom
{
    internal class KeyboardBuffer
    {
        public string text = "";

        public List<KeyPress> keyPresses = new List<KeyPress>();

        public KeyboardBuffer() { }

        public string Add(KeyPress keyPress)
        {
            keyPresses.Add(keyPress);
            var typed = keyPress.ToUnicode();
            text += typed;
            return typed;
        }

        public void Delete()
        {
            if (text.Length == 0)
                return;
            keyPresses.RemoveAt(keyPresses.Count - 1);
            text = text.Substring(0, text.Length - 1);
        }

        public void Reset()
        {
            keyPresses.Clear();
            text = "";
        }
    }
}
