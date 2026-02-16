using GT4.Core.Project.Dto;
using GT4.UI.Utils.Formatters;
using System.Globalization;
using Xunit;


namespace GT4.UI.Utils.Tests;

public class RelationshipTypeFormatterTests
{
  private readonly RelationshipTypeFormatter _formatter = new();

  private static Generation? ToGeneration(int? generation) =>
    generation.HasValue ? new Generation(generation.Value) : null;

  private static Consanguinity? ToConsanguinity(int? consanguinity) =>
    consanguinity.HasValue ? new Consanguinity(consanguinity.Value) : null;

  private static void SetLanguageCode(string code)
  {
    var culture = new CultureInfo(code);
    Thread.CurrentThread.CurrentCulture = culture;
    Thread.CurrentThread.CurrentUICulture = culture;
    CultureInfo.DefaultThreadCurrentCulture = culture;
    CultureInfo.DefaultThreadCurrentUICulture = culture;
  }

  private static void SetEn() => SetLanguageCode("EN");
  private static void SetRu() => SetLanguageCode("RU");

  [Theory]
  [InlineData(null, null, "Parent")]
  [InlineData(1, null, "Parent")]
  [InlineData(2, null, "Grandparent")]
  [InlineData(4, null, "Great-great-grandparent")]
  public void EN_UnknownSex_Parent(int? generation, int? consanguinity, string expected)
  {
    SetEn();
    var actual = _formatter.ToString(
      RelationshipType.Parent, 
      BiologicalSex.Unknown, 
      ToGeneration(generation), 
      ToConsanguinity(consanguinity));

    Assert.Equal(expected, actual);
  }
}
