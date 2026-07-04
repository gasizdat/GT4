namespace GT4.UI.DeviceTests;

internal static class TestStyles
{
  private static bool _Loaded;

  /// <summary>
  /// Installs the GT4 app style dictionaries into the runner application, mirroring UI\App\App.xaml.
  /// Must run on the UI thread before the first page is constructed. Colors go first: Styles.xaml
  /// resolves color keys through Application.Current during its own inflation.
  /// </summary>
  public static void EnsureLoaded()
  {
    if (_Loaded)
    {
      return;
    }

    var resources = Application.Current!.Resources;
    resources.MergedDictionaries.Add(new GT4TestColors());
    resources.MergedDictionaries.Add(new GT4TestStyles());
    _Loaded = true;
  }
}
