using System;
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
            if (!Path.GetFileName(filePath).Equals("Dead Space.exe", StringComparison.OrdinalIgnoreCase))
                return "Choose the original game's file named Dead Space.exe.";

            PeArchitecture architecture = GetArchitecture(filePath);
            if (architecture == PeArchitecture.X64)
                return "That is a 64-bit executable and is probably the 2023 remake. Choose Dead Space (2008), which is 32-bit.";
            if (architecture != PeArchitecture.X86)
                return "The file does not look like the 32-bit Dead Space (2008) executable.";
            return null;
        }
    }
}
