param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot "src"
$buildRoot = Join-Path $projectRoot "build"
$packageRoot = Join-Path $projectRoot "package"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
$programFilesX86 = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
$vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
$windowsKits = Join-Path $programFilesX86 "Windows Kits\10"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The Windows .NET Framework C# compiler was not found at $compiler"
}
if (-not (Test-Path -LiteralPath $vswhere)) { throw "Visual Studio Installer's vswhere.exe was not found." }
$visualStudio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) { throw "Visual C++ x86/x64 build tools are required to rebuild dinput8.dll." }
$msvcRoot = Get-ChildItem -LiteralPath (Join-Path $visualStudio "VC\Tools\MSVC") -Directory | Sort-Object Name -Descending | Select-Object -First 1
$sdkRoot = Get-ChildItem -LiteralPath (Join-Path $windowsKits "Include") -Directory | Where-Object {
    Test-Path (Join-Path $_.FullName "um\Windows.h")
} | Sort-Object Name -Descending | Select-Object -First 1
if (-not $msvcRoot -or -not $sdkRoot) { throw "A complete Visual C++ toolset and Windows 10/11 SDK are required." }
$nativeCompiler = Join-Path $msvcRoot.FullName "bin\Hostx64\x86\cl.exe"
$nativeInclude = $sdkRoot.FullName
$sdkVersion = $sdkRoot.Name
$nativeLib = Join-Path $windowsKits "Lib\$sdkVersion\um\x86"
$ucrtLib = Join-Path $windowsKits "Lib\$sdkVersion\ucrt\x86"
$msvcInclude = Join-Path $msvcRoot.FullName "include"
$msvcLib = Join-Path $msvcRoot.FullName "lib\x86"

New-Item -ItemType Directory -Force -Path $buildRoot, $packageRoot | Out-Null
$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
$output = Join-Path $buildRoot "DeadSpaceTextureLauncher.exe"
$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:x86",
    "/optimize+",
    "/checked+",
    "/warn:4",
    "/warnaserror+",
    "/win32manifest:$sourceRoot\app.manifest",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/reference:System.Windows.Forms.dll",
    "/out:$output"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }

$nativeOutput = Join-Path $buildRoot "dinput8.dll"
$priorInclude = $env:INCLUDE
$priorLib = $env:LIB
try {
    $env:INCLUDE = "$msvcInclude;$nativeInclude\ucrt;$nativeInclude\shared;$nativeInclude\um;$nativeInclude\winrt"
    $env:LIB = "$msvcLib;$nativeLib;$ucrtLib"
    & $nativeCompiler /nologo /LD /MT /O2 /W4 /WX /EHsc /DUNICODE /D_UNICODE `
        /Fe:$nativeOutput "$projectRoot\native\dinput8_proxy.cpp" `
        /link "/DEF:$projectRoot\native\dinput8_proxy.def" /NOLOGO /DYNAMICBASE /NXCOMPAT /IGNORE:4222
    if ($LASTEXITCODE -ne 0) { throw "Native bootstrap compilation failed with exit code $LASTEXITCODE" }
}
finally {
    $env:INCLUDE = $priorInclude
    $env:LIB = $priorLib
}

Copy-Item -LiteralPath $output -Destination (Join-Path $packageRoot "DeadSpaceTextureLauncher.exe") -Force
Copy-Item -LiteralPath $nativeOutput -Destination (Join-Path $packageRoot "dinput8.dll") -Force
Write-Host "Built $output"
Write-Host "Built $nativeOutput"
