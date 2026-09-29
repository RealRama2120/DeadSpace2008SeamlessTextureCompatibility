# Source build

Rama2120's code is MIT licensed. The release contains C# launcher source, native x86 DirectInput-forwarder source, manifests, and the PowerShell build script used for the packaged binaries.

Build requirements:

- Windows PowerShell 5.1 or PowerShell 7
- Windows .NET Framework 4.x compiler
- Visual Studio Build Tools with the Visual C++ x86/x64 component
- Windows 10 or 11 SDK

Run `build.ps1` from PowerShell. It locates the current Visual C++ toolset and Windows SDK, compiles `DeadSpaceTextureLauncher.exe` as x86, compiles `dinput8.dll` as x86 with the static C/C++ runtime, and copies both binaries to the project's package directory.

No TexMod binary, texture pack, game file, Microsoft runtime DLL, or Vortex extension source is part of this source tree.
