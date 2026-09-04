param(
  [Parameter(Mandatory = $true)][int]$ProcessId,
  [Parameter(Mandatory = $true)][string]$Out,
  [int]$Width = 0,
  [int]$Height = 0,
  [switch]$NoClientOnly
)

Add-Type -AssemblyName System.Drawing

if (-not ("Win32Capture" -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Win32Capture
{
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);

  [StructLayout(LayoutKind.Sequential)]
  public struct RECT { public int Left, Top, Right, Bottom; }

  public static int RectSize { get { return Marshal.SizeOf(typeof(RECT)); } }
}
'@
}

$proc = Get-Process -Id $ProcessId -ErrorAction Stop
$hwnd = $proc.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { throw "process $ProcessId has no main window yet" }

if ($Width -gt 0 -and $Height -gt 0) {
  [void][Win32Capture]::ShowWindow($hwnd, 9)   # SW_RESTORE
  [void][Win32Capture]::MoveWindow($hwnd, 40, 40, $Width, $Height, $true)
  Start-Sleep -Milliseconds 1200
}

[void][Win32Capture]::SetForegroundWindow($hwnd)

# Park the pointer on the title bar before shooting: a hovered control repaints in its hover
# colour and would otherwise be frozen mid-hover in the shot. It has to stay inside the window —
# moving the cursor right off it leaves the last hovered control stuck in its hover state.
$park = New-Object Win32Capture+RECT
[void][Win32Capture]::GetWindowRect($hwnd, [ref]$park)
[void][Win32Capture]::SetCursorPos(($park.Left + 800), ($park.Top + 16))
Start-Sleep -Milliseconds 900

$rect = New-Object Win32Capture+RECT
# DWMWA_EXTENDED_FRAME_BOUNDS = 9: the visible frame, without the invisible resize border.
if ([Win32Capture]::DwmGetWindowAttribute($hwnd, 9, [ref]$rect, [Win32Capture]::RectSize) -ne 0) {
  [void][Win32Capture]::GetWindowRect($hwnd, [ref]$rect)
}

$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
$bitmap = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
$ok = [Win32Capture]::PrintWindow($hwnd, $hdc, 2)
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()

$total = 0.0
$samples = 0
for ($y = 0; $y -lt $bitmap.Height; $y += 11) {
  for ($x = 0; $x -lt $bitmap.Width; $x += 11) {
    $c = $bitmap.GetPixel($x, $y)
    $total += ($c.R + $c.G + $c.B) / 3.0
    $samples++
  }
}

$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"printWindow=$ok {0}x{1} meanLuma={2:N1} -> {3}" -f $bitmap.Width, $bitmap.Height, ($total / $samples), $Out
$bitmap.Dispose()
