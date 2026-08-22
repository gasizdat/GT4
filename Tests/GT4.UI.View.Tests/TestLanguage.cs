using GT4.UI.Utils;
using System.Globalization;

namespace GT4.UI.View.Tests;

internal static class TestLanguage
{
  // Language.Current also writes CultureInfo.DefaultThreadCurrent*Culture, which is process-global
  // and races every other collection. Culture set here flows with the execution context instead, so
  // it stays inside the test that set it and the suite can run fully in parallel.
  public static void Use(Language language)
  {
    var culture = new CultureInfo(language.Code);
    CultureInfo.CurrentCulture = culture;
    CultureInfo.CurrentUICulture = culture;
  }
}
