using FluentAssertions;
using GT4.UI.Utils;
using System.Globalization;
using Xunit;

namespace GT4.UI.View.Tests;

[Collection<LanguageSwitchingTests>]
public class LanguageTests
{
  // MauiProgram and MainPage switch the whole app by assigning Language.Current, which only reaches
  // threads that never set a culture of their own if the setter writes the process-wide default.
  // Every other test deliberately goes through TestLanguage, so nothing else covers this.
  [Fact]
  public void Current_WhenAssigned_WritesTheProcessWideDefaultCulture()
  {
    var culture = CultureInfo.DefaultThreadCurrentCulture;
    var uiCulture = CultureInfo.DefaultThreadCurrentUICulture;

    try
    {
      Language.Current = Language.DE;

      CultureInfo.DefaultThreadCurrentCulture!.TwoLetterISOLanguageName.Should().Be(Language.DE.Code);
      CultureInfo.DefaultThreadCurrentUICulture!.TwoLetterISOLanguageName.Should().Be(Language.DE.Code);
    }
    finally
    {
      CultureInfo.DefaultThreadCurrentCulture = culture;
      CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
    }
  }
}
