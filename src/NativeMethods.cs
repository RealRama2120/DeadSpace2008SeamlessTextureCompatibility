using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DeadSpaceTextureLauncher
{
    internal static class NativeMethods
    {
        internal delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);

        internal const uint BM_CLICK = 0x00F5;
        internal const uint WM_CLOSE = 0x0010;
        internal const uint WM_GETTEXT = 0x000D;
        internal const uint WM_SETTEXT = 0x000C;
        internal const uint WM_KEYDOWN = 0x0100;
        internal const uint WM_KEYUP = 0x0101;
        internal const uint LVM_GETITEMCOUNT = 0x1004;
        internal const int SW_HIDE = 0;
        internal const int SW_RESTORE = 9;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOACTIVATE = 0x0010;
        private const uint TH32CS_SNAPPROCESS = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExecutableFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        internal static extern bool EnumChildWindows(IntPtr parent, EnumWindowProc callback, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr window, StringBuilder className, int maximum);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, StringBuilder lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        internal static extern int GetDlgCtrlID(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDlgItem(IntPtr dialog, int id);

        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowEnabled(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachInput);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        internal static string WindowText(IntPtr window)
        {
            StringBuilder text = new StringBuilder(2048);
            SendMessage(window, WM_GETTEXT, new IntPtr(text.Capacity), text);
            return text.ToString();
        }

        internal static string WindowClass(IntPtr window)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(window, className, className.Capacity);
            return className.ToString();
        }

        internal static List<IntPtr> ChildWindows(IntPtr parent, string className)
        {
            List<IntPtr> children = new List<IntPtr>();
            EnumChildWindows(parent, delegate(IntPtr window, IntPtr parameter)
            {
                if (ClassMatches(WindowClass(window), className))
                    children.Add(window);
                return true;
            }, IntPtr.Zero);
            return children;
        }

        internal static IntPtr ChildById(IntPtr parent, int id, string className)
        {
            IntPtr found = IntPtr.Zero;
            EnumChildWindows(parent, delegate(IntPtr window, IntPtr parameter)
            {
                if (GetDlgCtrlID(window) == id &&
                    ClassMatches(WindowClass(window), className))
                {
                    found = window;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        internal static IntPtr TopWindow(uint processId, string className, bool visibleOnly)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != processId) return true;
                if (visibleOnly && !IsWindowVisible(window)) return true;
                if (!string.IsNullOrEmpty(className) && !WindowClass(window).Equals(className, StringComparison.OrdinalIgnoreCase)) return true;
                found = window;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        internal static List<IntPtr> TopWindows(uint processId)
        {
            List<IntPtr> windows = new List<IntPtr>();
            EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner == processId) windows.Add(window);
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        internal static void Click(IntPtr button)
        {
            if (button != IntPtr.Zero) PostMessage(button, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }

        private static bool ClassMatches(string actual, string requested)
        {
            if (string.IsNullOrEmpty(requested)) return true;
            if (actual.Equals(requested, StringComparison.OrdinalIgnoreCase)) return true;
            // WinForms adds a versioned wrapper around standard Win32 classes. This
            // branch is used by the local mock integration test, not by TexMod 0.9b.
            return requested.Equals("Button", StringComparison.OrdinalIgnoreCase) &&
                   actual.IndexOf(".BUTTON.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void ForceForeground(IntPtr window)
        {
            if (window == IntPtr.Zero) return;
            IntPtr foreground = GetForegroundWindow();
            uint foregroundProcess;
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out foregroundProcess);
            uint currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
            ShowWindow(window, SW_RESTORE);
            BringWindowToTop(window);
            SetForegroundWindow(window);
            if (attached) AttachThreadInput(currentThread, foregroundThread, false);
        }

        internal static List<int> ChildProcessIds(int parentProcessId, string executableFile)
        {
            List<int> ids = new List<int>();
            if (parentProcessId <= 0) return ids;
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == new IntPtr(-1)) return ids;
            try
            {
                ProcessEntry entry = new ProcessEntry();
                entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry));
                if (!Process32FirstW(snapshot, ref entry)) return ids;
                do
                {
                    if (entry.ParentProcessId == (uint)parentProcessId &&
                        string.Equals(entry.ExecutableFile, executableFile, StringComparison.OrdinalIgnoreCase))
                        ids.Add((int)entry.ProcessId);
                    entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry));
                }
                while (Process32NextW(snapshot, ref entry));
            }
            finally { CloseHandle(snapshot); }
            return ids;
        }
    }
}
