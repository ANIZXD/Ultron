param(
    [string]$ProcessName = "Ultron",
    [string]$Output = "C:\Users\ay660\ultron\ultron-window.png"
)

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CaptureWin {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

[CaptureWin]::SetProcessDPIAware() | Out-Null

$process = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$handle = $process.MainWindowHandle
if ($handle -eq [IntPtr]::Zero) {
    throw "Process $ProcessName has no main window."
}

[CaptureWin]::ShowWindow($handle, 9) | Out-Null
[CaptureWin]::SetWindowPos($handle, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002 -bor 0x0040) | Out-Null
[CaptureWin]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 700

$rect = New-Object CaptureWin+RECT
[CaptureWin]::GetWindowRect($handle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $width, $height))
$bitmap.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

Write-Output ("PID=" + $process.Id + " RECT=" + $rect.Left + "," + $rect.Top + " " + $width + "x" + $height + " DPI=" + [CaptureWin]::GetDpiForWindow($handle) + " IMAGE=" + $Output)
