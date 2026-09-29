# Building Dead Space (2008) Seamless Texture Compatibility v2.1.2

These instructions build Rama2120's launcher and DirectInput bootstrap from the browsable v2.1.2 source at this repository's root. They do not build Dead Space, TexMod, or any third-party texture pack. You do not need the game installed to compile the two binaries.

## Source and build inputs

- `src/*.cs` and `src/app.manifest`: the Windows Forms launcher.
- `native/dinput8_proxy.cpp` and `native/dinput8_proxy.def`: the x86 DirectInput forwarding/bootstrap DLL.
- `build.ps1`: the build script; it locates the toolchains, invokes both compilers, and stages the two resulting binaries.
- `DeadSpaceTextureLauncher.vortex.json` and `Documentation/`: deployment metadata and player-facing files. They are not compiler inputs.

The unmodified v2.1.2 install ZIP and matching source ZIP are in [`versions/v2.1.2`](versions/v2.1.2). `SOURCE_CHECKSUMS.txt` came from that source ZIP. This guide is an additional reviewer document, not a file from the original source ZIP.

## Prerequisites

Build on **64-bit Windows** with:

1. Windows PowerShell 5.1 or newer PowerShell capable of running `build.ps1`.
2. The .NET Framework 4.x C# compiler at `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe`. The script uses this compiler directly; it does not use the `dotnet` SDK, NuGet, or MSBuild.
3. Visual Studio or Visual Studio Build Tools with the **MSVC x86/x64 C++ build tools** component (`Microsoft.VisualStudio.Component.VC.Tools.x86.x64`). The script finds the installation with Visual Studio Installer's `vswhere.exe`.
4. A Windows 10 or 11 SDK with headers and x86 UM/UCRT libraries. The script selects the newest installed SDK whose `Include/<version>/um/Windows.h` exists.

The script expects MSVC's `bin/Hostx64/x86/cl.exe`, so an x64 host toolchain and x86 target tools must both be installed. It reads the compilers and SDK from their normal installation locations and writes only within this checkout. Administrator privileges are not required when the checkout is writable.

## Build

From a local clone or extracted copy of this repository, open PowerShell in the repository root and run:

```powershell
.\build.ps1
```

For example, a reviewer using Git can obtain the exact source and run the script with:

```powershell
git clone https://github.com/RealRama2120/DeadSpace2008SeamlessTextureCompatibility.git
Set-Location .\DeadSpace2008SeamlessTextureCompatibility
.\build.ps1
```

If your local PowerShell policy blocks scripts, follow your organization's policy. On a machine where a process-scoped `RemoteSigned` policy is permitted, this invocation works without changing the user or machine policy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\build.ps1
```

The script compiles `src/*.cs` in filename order with the .NET Framework compiler using `/target:winexe`, `/platform:x86`, and optimization. It then compiles the native proxy as a 32-bit DLL using MSVC and the checked-in `.def` export list. Compiler failure stops the script with an error rather than creating a purported successful package.

## Expected output and checks

A successful run prints `Built` lines for both binaries and creates:

| Path | Purpose |
| --- | --- |
| `build/DeadSpaceTextureLauncher.exe` | Compiled 32-bit launcher |
| `build/dinput8.dll` | Compiled 32-bit DirectInput bootstrap |
| `package/DeadSpaceTextureLauncher.exe` | Copy staged for installation |
| `package/dinput8.dll` | Copy staged for installation |

Use these PowerShell checks after the build:

```powershell
Test-Path .\build\DeadSpaceTextureLauncher.exe, .\build\dinput8.dll, .\package\DeadSpaceTextureLauncher.exe, .\package\dinput8.dll
Get-FileHash .\build\DeadSpaceTextureLauncher.exe, .\build\dinput8.dll -Algorithm SHA256
```

All four `Test-Path` results should be `True`. The script may also leave native intermediate files such as `.obj`, `.lib`, and `.exp`; these are not install files. `package/` contains only the two compiled binaries. The release ZIP additionally includes the Vortex manifest and documentation, so `build.ps1` alone does **not** recreate the complete archive.

The archived v2.1.2 install ZIP has SHA-256 `780A7C6A8B7256F6B700585CA3AC596CADDCEB01F78E1D408637C43C182A2D9B`. That hash identifies the preserved release ZIP, **not** the output of a new build. Compiler/SDK versions can change binary bytes; this project does not claim bit-for-bit reproducible builds. Compare behavior and source rather than expecting new EXE/DLL hashes to equal the archived release.

## If the build stops

- Missing `csc.exe`: ensure the Windows .NET Framework 4.x compiler is present at the path above.
- Missing `vswhere.exe` or Visual C++ tools: install Visual Studio/Build Tools with the MSVC x86/x64 component.
- Missing SDK headers or x86 libraries: install a complete Windows 10/11 SDK.
- Native compilation errors: confirm the x64-host/x86-target MSVC toolchain and SDK x86 libraries are installed together; retain the compiler output when reporting the failure.

No build step downloads TexMod, modifies antivirus settings, edits a game installation, or contacts an external service. The original release archives are reference artifacts and are not overwritten by this script.
