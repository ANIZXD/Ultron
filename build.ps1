$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$speech = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech\v4.0_4.0.0.0__31bf3856ad364e35\System.Speech.dll'
$winmd = Join-Path $env:WINDIR 'System32\WinMetadata'

if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }
if (-not (Test-Path $speech)) { throw "System.Speech.dll not found at $speech" }
if (-not (Test-Path $winmd)) { throw "Windows WinRT metadata not found at $winmd" }

$references = @(
    "/reference:`"$speech`"",
    "/reference:`"$framework\System.Management.dll`"",
    "/reference:`"$framework\System.Web.Extensions.dll`"",
    "/reference:`"$framework\System.Net.Http.dll`"",
    "/reference:`"$framework\System.Windows.Forms.dll`"",
    "/reference:`"$framework\System.Drawing.dll`"",
    "/reference:`"$winmd\Windows.Foundation.winmd`"",
    "/reference:`"$winmd\Windows.Globalization.winmd`"",
    "/reference:`"$winmd\Windows.Media.winmd`"",
    "/reference:`"$framework\System.Runtime.WindowsRuntime.dll`"",
    "/reference:`"$framework\System.Runtime.dll`"",
    "/reference:System.dll",
    "/reference:System.Core.dll"
)

$uiSources = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs -File | ForEach-Object { $_.FullName })
$coreSources = @(Get-ChildItem (Join-Path $root 'src\Core') -Filter *.cs -File | ForEach-Object { $_.FullName })

Write-Host "Building Ultron.exe..."
& $csc /nologo /target:winexe /out:"$root\Ultron.exe" /langversion:5 /codepage:65001 $references $uiSources $coreSources
if ($LASTEXITCODE -ne 0) { throw "Ultron UI build failed." }

Write-Host "Building UltronTests.exe..."
& $csc /nologo /target:exe /out:"$root\UltronTests.exe" /langversion:5 /codepage:65001 $references (Join-Path $root 'tests\UltronTests.cs') $coreSources
if ($LASTEXITCODE -ne 0) { throw "Ultron test build failed." }

# Some Windows 11 machines with Smart App Control block unsigned console
# executables. Build the same harness as a GUI app as a fallback; pass
# --out <file> to capture its report.
Write-Host "Building UltronTestsWin.exe (Smart App Control fallback)..."
& $csc /nologo /target:winexe /out:"$root\UltronTestsWin.exe" /langversion:5 /codepage:65001 $references (Join-Path $root 'tests\UltronTests.cs') $coreSources
if ($LASTEXITCODE -ne 0) { throw "Ultron GUI test build failed." }

Write-Host "Build complete."
Write-Host "  $root\Ultron.exe"
Write-Host "  $root\UltronTests.exe"
Write-Host "  $root\UltronTestsWin.exe"
