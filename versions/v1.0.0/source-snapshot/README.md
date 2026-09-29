# Building from source

The launcher uses only the Windows .NET Framework class library and Win32 APIs. No NuGet packages or network download are required.

On a normal Windows installation with .NET Framework 4.x:

1. Open PowerShell in this `Source` folder.
2. Run `powershell -ExecutionPolicy Bypass -File .\build-source.ps1`.
3. The rebuilt `DeadSpaceTextureLauncher.exe` appears one folder above `Source`.

The release is intentionally compiled as x86 because Dead Space (2008) and TexMod 0.9b are 32-bit applications. The launcher uses `asInvoker`; it does not request administrator access.

Source license: MIT, copyright 2026 Rama2120.
