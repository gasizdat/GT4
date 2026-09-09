using GT4.Core.Project.Abstraction;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace GT4.UI;

// Gives the unpackaged win-x64 build (WindowsPackageType=None, GT4-<version>-win-x64.zip) the same
// .gt4 double-click association the MSIX build declares in Package.appxmanifest. There is no
// manifest for an unpackaged app to declare it in, so it registers itself instead.
internal static class ProjectFileAssociation
{
  private const string ProgId = "GT4.Project";
  private const int ShcneAssocChanged = 0x08000000;
  private const int ShcnfIdlist = 0x0000;

  public static void EnsureRegisteredIfUnpackaged()
  {
    if (IsPackaged() || Environment.ProcessPath is not string exePath)
    {
      return;
    }

    var command = $"\"{exePath}\" \"%1\"";
    using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProjectFileExtensions.Gt4Extension}");
    using var commandKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}\shell\open\command");
    if ((string?)extensionKey.GetValue(null) == ProgId && (string?)commandKey?.GetValue(null) == command)
    {
      // Already pointing at this exe; re-registering on every launch would just churn the registry.
      return;
    }

    extensionKey.SetValue(null, ProgId);
    using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
    progIdKey.SetValue(null, "GT4 Project File");
    using (var iconKey = progIdKey.CreateSubKey("DefaultIcon"))
    {
      iconKey.SetValue(null, $"{exePath},0");
    }
    using (var openCommandKey = progIdKey.CreateSubKey(@"shell\open\command"))
    {
      openCommandKey.SetValue(null, command);
    }

    SHChangeNotify(ShcneAssocChanged, ShcnfIdlist, IntPtr.Zero, IntPtr.Zero);
  }

  private static bool IsPackaged()
  {
    try
    {
      _ = Windows.ApplicationModel.Package.Current;
      return true;
    }
    catch (InvalidOperationException)
    {
      return false;
    }
  }

  [DllImport("shell32.dll")]
  private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
