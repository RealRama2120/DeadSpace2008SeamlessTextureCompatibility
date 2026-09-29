# Building from source

The launcher and EA proxy use only the Windows .NET Framework class library and Win32 APIs. No NuGet packages or build-time network download are required.

On a normal Windows installation with .NET Framework 4.x:

1. Open PowerShell in this `Source` folder.
2. Run `powershell -ExecutionPolicy Bypass -File .\build-source.ps1`.
3. The rebuilt `DeadSpaceTextureLauncher.exe` and `DeadSpaceTextureProxy.exe` appear one folder above `Source`.

Both binaries are intentionally x86 because Dead Space (2008) and TexMod 0.9b are 32-bit applications. Both manifests use `asInvoker`. EA integration invokes a special verified helper mode with Windows `runas` only for its one-time game-folder rename/copy operation.

`Proxy/ProxyProgram.cs` builds the separate executable proxy. It is not a DLL and does not hook controller or graphics APIs.

Source license: MIT, copyright 2026 Rama2120.
