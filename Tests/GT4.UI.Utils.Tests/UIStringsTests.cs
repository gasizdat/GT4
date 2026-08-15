using GT4.UI.Resources;
using System.Collections;
using System.Globalization;
using Xunit;

namespace GT4.UI.Utils.Tests;

public class UIStringsTests
{
  private static string[] KeysOf(CultureInfo culture)
  {
    var set = UIStrings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
    Assert.NotNull(set);
    var entries = set.Cast<DictionaryEntry>();
    var keys = entries.Select(e => (string)e.Key);

    return keys.Order().ToArray();
  }

  public static TheoryData<string> TranslatedCultures =>
    new(Language.Languages.Where(l => l != Language.Default).Select(l => l.Code));

  // A key absent from a satellite silently resolves to the neutral (English) value at runtime, so
  // a partially translated UI would ship without any build or test failure.
  [Theory]
  [MemberData(nameof(TranslatedCultures))]
  public void EverySatelliteCarriesEveryNeutralKey(string code)
  {
    var neutral = KeysOf(CultureInfo.InvariantCulture);
    var translated = KeysOf(new CultureInfo(code));

    Assert.NotEmpty(neutral);
    Assert.Equal(neutral, translated);
  }

  [Theory]
  [MemberData(nameof(TranslatedCultures))]
  public void NoSatelliteValueIsBlank(string code)
  {
    var set = UIStrings.ResourceManager.GetResourceSet(new CultureInfo(code), createIfNotExists: true, tryParents: false);
    var entries = set!.Cast<DictionaryEntry>();
    var blank = entries.Where(e => string.IsNullOrWhiteSpace((string?)e.Value)).Select(e => (string)e.Key);

    Assert.Empty(blank);
  }

  [Theory]
  [InlineData("Month_03", "März")]
  [InlineData("RelGrandFather", "Großvater")]
  public void GermanSatelliteResolves(string key, string expected)
  {
    var german = new CultureInfo(Language.DE.Code);
    var actual = UIStrings.ResourceManager.GetString(key, german);

    Assert.Equal(expected, actual);
  }
}
