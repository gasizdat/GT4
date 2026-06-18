using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Globalization;
using Xunit;

namespace GT4.UI.View.Tests;

public class FontScaleSettingTests
{
  private const string FontScaleSection = "Appearance.FontScale";

  private static FontScaleSetting Make(
    string? configuredValue = null,
    IInteractiveConfiguration? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[FontScaleSection]).Returns(configuredValue);
    return new FontScaleSetting(config.Object, interactive);
  }

  [Fact]
  public void Value_WhenNotConfigured_FallsBackToDefaultFactor()
  {
    Make().Value.Should().Be(FontScaleSetting.DefaultFactor);
  }

  [Fact]
  public void Value_WhenConfigured_ReturnsParsedFactor()
  {
    Make(configuredValue: "1.25").Value.Should().Be(1.25);
  }

  [Fact]
  public void Value_AlwaysUsesInvariantCulture()
  {
    // A persisted value uses '.' as the decimal separator regardless of the current culture.
    var previous = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
      Make(configuredValue: "1.5").Value.Should().Be(1.5);
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }
  }

  [Fact]
  public void Value_WhenConfiguredValueUnparseable_FallsBackToDefaultFactor()
  {
    Make(configuredValue: "not-a-number").Value.Should().Be(FontScaleSetting.DefaultFactor);
  }

  [Fact]
  public void Value_WhenConfiguredAboveMaximum_IsClampedToMaximum()
  {
    Make(configuredValue: "5.0").Value.Should().Be(FontScaleSetting.MaxFactor);
  }

  [Fact]
  public void Value_WhenConfiguredBelowMinimum_IsClampedToMinimum()
  {
    Make(configuredValue: "0.1").Value.Should().Be(FontScaleSetting.MinFactor);
  }

  [Fact]
  public void SetValue_WithInteractiveConfig_PersistsClampedInvariantString()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = 3.0;

    interactive.Verify(
      i => i.SetKey(
        FontScaleSection,
        FontScaleSetting.MaxFactor.ToString(CultureInfo.InvariantCulture)),
      Times.Once);
  }

  [Fact]
  public void ResetToDefault_CallsRemoveKeyWithFontScaleSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(FontScaleSection), Times.Once);
  }
}
