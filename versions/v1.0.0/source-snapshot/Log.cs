using System;
using System.IO;
using System.Text;

namespace DeadSpaceTextureLauncher
{
    internal static class Log
    {
        private static readonly object Gate = new object();

        public static void Start()
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            lock (Gate)
            {
                File.WriteAllText(AppInfo.LogPath,
                    "Dead Space Texture Launcher " + AppInfo.Version + Environment.NewLine +
                    "Started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") + Environment.NewLine +
                    "Windows: " + Environment.OSVersion + Environment.NewLine +
                    "64-bit OS: " + Environment.Is64BitOperatingSystem + Environment.NewLine +
                    Environment.NewLine,
                    new UTF8Encoding(false));
            }
        }

        public static void Info(string message) { Write("INFO", message); }
        public static void Warn(string message) { Write("WARN", message); }
        public static void Error(string message) { Write("ERROR", message); }

        private static void Write(string level, string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + level + ": " + message;
            lock (Gate)
            {
                try { File.AppendAllText(AppInfo.LogPath, line + Environment.NewLine, new UTF8Encoding(false)); }
                catch { }
            }
        }
    }
}
