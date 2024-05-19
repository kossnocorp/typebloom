using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TypeBloom
{
    internal class OS
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        static public string GetFocusedName()
        {
            IntPtr handle = GetForegroundWindow();

            // Get the window title
            StringBuilder windowTitle = new StringBuilder(256);
            if (GetWindowText(handle, windowTitle, windowTitle.Capacity) > 0)
            {
                Debug.WriteLine("Focused Window Title: " + windowTitle);
            }

            // Get the process ID
            GetWindowThreadProcessId(handle, out uint processId);

            // Get the process name
            Process process = Process.GetProcessById((int)processId);
            Debug.WriteLine("Focused Application: " + process.ProcessName);

            return process.ProcessName;
        }
    }
}
