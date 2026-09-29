$ErrorActionPreference = "Stop"
$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$output = Join-Path (Split-Path -Parent $sourceRoot) "DeadSpaceTextureLauncher.exe"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The Windows .NET Framework C# compiler was not found at $compiler"
}

$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
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
    "/reference:System.Windows.Forms.dll",
    "/out:$output"
) + $sources

& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }
Write-Host "Built $output"
