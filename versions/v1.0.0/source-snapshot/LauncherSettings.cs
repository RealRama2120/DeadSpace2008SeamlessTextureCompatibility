using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DeadSpaceTextureLauncher
{
    internal sealed class LauncherSettings
    {
        public string GamePath = string.Empty;
        public string TexModPath = string.Empty;
        public readonly List<string> Packages = new List<string>();
        public bool KeepTexModOutOfSight = true;
        public bool CloseTexModWithGame = true;
        public int ActionDelayMs = 400;
        public int GameWindowTimeoutMinutes = 10;

        public bool IsComplete
        {
            get
            {
                return File.Exists(GamePath) &&
                       File.Exists(TexModPath) &&
                       Packages.Count > 0 &&
                       Packages.TrueForAll(File.Exists);
            }
        }

        public LauncherSettings Clone()
        {
            LauncherSettings copy = new LauncherSettings();
            copy.GamePath = GamePath;
            copy.TexModPath = TexModPath;
            copy.Packages.AddRange(Packages);
            copy.KeepTexModOutOfSight = KeepTexModOutOfSight;
            copy.CloseTexModWithGame = CloseTexModWithGame;
            copy.ActionDelayMs = ActionDelayMs;
            copy.GameWindowTimeoutMinutes = GameWindowTimeoutMinutes;
            return copy;
        }
    }

    internal static class SettingsStore
    {
        public static LauncherSettings Load()
        {
            LauncherSettings settings = new LauncherSettings();
            if (!File.Exists(AppInfo.SettingsPath))
                return settings;

            foreach (string raw in File.ReadAllLines(AppInfo.SettingsPath, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                    continue;

                int separator = line.IndexOf('=');
                if (separator < 1)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (key.Equals("GamePath", StringComparison.OrdinalIgnoreCase))
                    settings.GamePath = value;
                else if (key.Equals("TexModPath", StringComparison.OrdinalIgnoreCase))
                    settings.TexModPath = value;
                else if (key.StartsWith("Package", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                    settings.Packages.Add(value);
                else if (key.Equals("KeepTexModOutOfSight", StringComparison.OrdinalIgnoreCase))
                    settings.KeepTexModOutOfSight = ParseBool(value, true);
                else if (key.Equals("CloseTexModWithGame", StringComparison.OrdinalIgnoreCase))
                    settings.CloseTexModWithGame = ParseBool(value, true);
                else if (key.Equals("ActionDelayMs", StringComparison.OrdinalIgnoreCase))
                    settings.ActionDelayMs = Clamp(ParseInt(value, 400), 150, 3000);
                else if (key.Equals("GameWindowTimeoutMinutes", StringComparison.OrdinalIgnoreCase))
                    settings.GameWindowTimeoutMinutes = Clamp(ParseInt(value, 10), 2, 30);
            }

            RemoveDuplicatePackages(settings.Packages);
            return settings;
        }

        public static void Save(LauncherSettings settings)
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            StringBuilder text = new StringBuilder();
            text.AppendLine("# Dead Space Texture Launcher " + AppInfo.Version);
            text.AppendLine("# User-selected files are referenced only; they are never copied or redistributed.");
            text.AppendLine("GamePath=" + Clean(settings.GamePath));
            text.AppendLine("TexModPath=" + Clean(settings.TexModPath));
            text.AppendLine("KeepTexModOutOfSight=" + (settings.KeepTexModOutOfSight ? "1" : "0"));
            text.AppendLine("CloseTexModWithGame=" + (settings.CloseTexModWithGame ? "1" : "0"));
            text.AppendLine("ActionDelayMs=" + settings.ActionDelayMs);
            text.AppendLine("GameWindowTimeoutMinutes=" + settings.GameWindowTimeoutMinutes);
            for (int i = 0; i < settings.Packages.Count; i++)
                text.AppendLine("Package" + (i + 1) + "=" + Clean(settings.Packages[i]));

            string temporary = AppInfo.SettingsPath + ".tmp";
            File.WriteAllText(temporary, text.ToString(), new UTF8Encoding(false));
            if (File.Exists(AppInfo.SettingsPath))
                File.Replace(temporary, AppInfo.SettingsPath, null);
            else
                File.Move(temporary, AppInfo.SettingsPath);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static void RemoveDuplicatePackages(List<string> packages)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = packages.Count - 1; i >= 0; i--)
            {
                string full;
                try { full = Path.GetFullPath(packages[i]); }
                catch { packages.RemoveAt(i); continue; }
                if (!seen.Add(full)) packages.RemoveAt(i);
                else packages[i] = full;
            }
        }
    }
}
