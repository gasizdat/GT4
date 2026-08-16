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

  [Theory]
  [InlineData("Month_03", "marzo")]
  [InlineData("RelGrandFather", "Abuelo")]
  public void SpanishSatelliteResolves(string key, string expected)
  {
    var spanish = new CultureInfo(Language.ES.Code);
    var actual = UIStrings.ResourceManager.GetString(key, spanish);

    Assert.Equal(expected, actual);
  }

  [Theory]
  [InlineData("Month_03", "mars")]
  [InlineData("RelGrandFather", "Grand-père")]
  public void FrenchSatelliteResolves(string key, string expected)
  {
    var french = new CultureInfo(Language.FR.Code);
    var actual = UIStrings.ResourceManager.GetString(key, french);

    Assert.Equal(expected, actual);
  }

  // The Spanish formatter names a generation by prefixing the grandparent/grandchild stem where it
  // sits inside the composed term. Rewording one of these so it no longer spells the stem leaves
  // every deeper generation rendering exactly like the shallowest, with nothing thrown.
  [Theory]
  [InlineData("RelGrandParent", "RelGrandFather")]
  [InlineData("RelGrandParent", "RelGrandMother")]
  [InlineData("RelGrandChild", "RelGrandSon")]
  [InlineData("RelGrandChild", "RelGrandDaughter")]
  [InlineData("RelGrandUncle_en", "RelGrandFather")]
  [InlineData("RelGrandAunt_en", "RelGrandMother")]
  [InlineData("RelGrandUncleAunt_en", "RelGrandFather")]
  [InlineData("RelGrandUncleAunt_en", "RelGrandMother")]
  [InlineData("RelGrandNephew_en", "RelGrandSon")]
  [InlineData("RelGrandNiece_en", "RelGrandDaughter")]
  [InlineData("RelGrandNephewNiece_en", "RelGrandSon")]
  [InlineData("RelGrandNephewNiece_en", "RelGrandDaughter")]
  public void SpanishComposedTermSpellsItsStem(string composedKey, string stemKey)
  {
    var spanish = new CultureInfo(Language.ES.Code);
    var composed = UIStrings.ResourceManager.GetString(composedKey, spanish);
    var stem = UIStrings.ResourceManager.GetString(stemKey, spanish);

    Assert.NotNull(composed);
    Assert.NotNull(stem);
    Assert.Contains(stem, composed, StringComparison.OrdinalIgnoreCase);
  }

  // The Spanish formatter reads the letter before the placeholder to decide whether the prefix
  // contracts with the stem, so a template that opened with its placeholder would throw.
  [Theory]
  [InlineData("RelGreat_1")]
  [InlineData("RelGreat2_es_1")]
  public void SpanishGenerationPrefixLeadsItsPlaceholder(string key)
  {
    var spanish = new CultureInfo(Language.ES.Code);
    var template = UIStrings.ResourceManager.GetString(key, spanish);

    Assert.NotNull(template);
    var seam = template.IndexOf("{0}", StringComparison.Ordinal);

    Assert.True(seam > 0, $"'{key}' must spell a prefix before its placeholder, but was '{template}'.");
  }
}
