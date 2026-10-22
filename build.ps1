# Builds dist\ClaudeAccountSwitch.exe with the C# compiler that ships with
# Windows (.NET Framework 4.x). No SDK or Visual Studio required.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe (.NET Framework 4.x) not found.' }

$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $build, $dist | Out-Null

$ico = Join-Path $build 'app.ico'
& (Join-Path $root 'tools\make-icon.ps1') -Out $ico

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$out = Join-Path $dist 'ClaudeAccountSwitch.exe'

& $csc /nologo /target:winexe /codepage:65001 /optimize+ /platform:anycpu `
    "/out:$out" "/win32icon:$ico" "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

Write-Host "Built $out"
