using System;
using System.IO;

namespace DeadSpaceTextureLauncher
{
    internal static class AppInfo
    {
        public const string Name = "Dead Space Texture Launcher";
        public const string Version = "2.1.0";
        public const string Author = "Rama2120";

        public static string ExecutableDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string DataDirectory
        {
            get
            {
                string overrideDirectory = Environment.GetEnvironmentVariable("DSTL_CONFIG_DIR");
                if (!string.IsNullOrWhiteSpace(overrideDirectory))
                    return Path.GetFullPath(overrideDirectory);

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Rama2120",
                    "DeadSpaceTextureLauncher");
            }
        }

        public static string SettingsPath { get { return Path.Combine(DataDirectory, "settings.ini"); } }
        public static string LogPath { get { return Path.Combine(DataDirectory, "latest.log"); } }
        public static string ToolsDirectory { get { return Path.Combine(DataDirectory, "Tools"); } }
        public static string ManagedTexModPath { get { return Path.Combine(ToolsDirectory, "TexMod.exe"); } }

        public static bool TestMode
        {
            get { return string.Equals(Environment.GetEnvironmentVariable("DSTL_TEST_MODE"), "1", StringComparison.Ordinal); }
        }
    }
}
