param(
  [Parameter(Mandatory = $true)][int]$ProcessId,
  [Parameter(Mandatory = $true)][int]$X,
  [Parameter(Mandatory = $true)][int]$Y,
  [int]$WaitMs = 2500,
  [string]$Out = ''
)

if (-not ("Win32Input" -as [type])) {
  Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class Win32Input
{
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);

  [StructLayout(LayoutKind.Sequential)]
  public struct RECT { public int Left, Top, Right, Bottom; }

  public static int RectSize { get { return Marshal.SizeOf(typeof(RECT)); } }

  public const uint LeftDown = 0x0002;
  public const uint LeftUp = 0x0004;
}
'@
}

$proc = Get-Process -Id $ProcessId -ErrorAction Stop
$hwnd = $proc.MainWindowHandle
[void][Win32Input]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 350

# The window rect, not the DWM frame: PrintWindow renders from the window origin, so image
# coordinates line up with the window rect and are offset from the frame by the resize border.
$rect = New-Object Win32Input+RECT
[void][Win32Input]::GetWindowRect($hwnd, [ref]$rect)

$screenX = $rect.Left + $X
$screenY = $rect.Top + $Y
# Approach in two steps: WinUI hit-tests hover before it accepts a press, so a cursor that
# teleports onto a control and clicks in the same instant is ignored by the smaller targets.
[void][Win32Input]::SetCursorPos($screenX - 6, $screenY - 6)
Start-Sleep -Milliseconds 250
[void][Win32Input]::SetCursorPos($screenX, $screenY)
Start-Sleep -Milliseconds 400
[Win32Input]::mouse_event([Win32Input]::LeftDown, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 140
[Win32Input]::mouse_event([Win32Input]::LeftUp, 0, 0, 0, [UIntPtr]::Zero)
"clicked image($X,$Y) -> screen($screenX,$screenY)"

Start-Sleep -Milliseconds $WaitMs

if ($Out -ne '') {
  & (Join-Path $PSScriptRoot 'capture.ps1') -ProcessId $ProcessId -Out $Out
}
