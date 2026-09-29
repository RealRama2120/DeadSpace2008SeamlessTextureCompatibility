using System;
using System.Diagnostics;
using System.IO;

namespace DeadSpaceTextureLauncher
{
    internal enum PeArchitecture
    {
        Unknown,
        X86,
        X64
    }

    internal static class PeInspector
    {
        public static PeArchitecture GetArchitecture(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (stream.Length < 256 || reader.ReadUInt16() != 0x5A4D)
                        return PeArchitecture.Unknown;
                    stream.Position = 0x3C;
                    int peOffset = reader.ReadInt32();
                    if (peOffset < 64 || peOffset > stream.Length - 24)
                        return PeArchitecture.Unknown;
                    stream.Position = peOffset;
                    if (reader.ReadUInt32() != 0x00004550)
                        return PeArchitecture.Unknown;
                    ushort machine = reader.ReadUInt16();
                    if (machine == 0x014C) return PeArchitecture.X86;
                    if (machine == 0x8664) return PeArchitecture.X64;
                    return PeArchitecture.Unknown;
                }
            }
            catch
            {
                return PeArchitecture.Unknown;
            }
        }

        public static string ValidateDeadSpace2008(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return "Dead Space.exe was not found.";
            string fileName = Path.GetFileName(filePath);
            if (!fileName.Equals("Dead Space.exe", StringComparison.OrdinalIgnoreCase) &&
                !fileName.Equals("Dead Space.original.exe", StringComparison.OrdinalIgnoreCase))
                return "Choose Dead Space.exe or the launcher's verified Dead Space.original.exe backup.";

            try
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
                if (string.Equals(version.CompanyName, AppInfo.Author, StringComparison.OrdinalIgnoreCase) ||
                    (version.ProductName ?? string.Empty).IndexOf("Texture Proxy", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "That file is the texture-launch proxy, not the original game executable.";
            }
            catch { }

            PeArchitecture architecture = GetArchitecture(filePath);
            if (architecture == PeArchitecture.X64)
                return "That is a 64-bit executable and is probably the 2023 remake. Choose Dead Space (2008), which is 32-bit.";
            if (architecture != PeArchitecture.X86)
                return "The file does not look like the 32-bit Dead Space (2008) executable.";
            return null;
        }

        public static bool IsRamaProxy(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(filePath);
                return string.Equals(version.CompanyName, AppInfo.Author, StringComparison.OrdinalIgnoreCase) &&
                       (version.ProductName ?? string.Empty).IndexOf("Texture Proxy", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }
    }
}
