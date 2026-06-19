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
    IInteractiveConfiguration? interactive = null,
    FontScale? fontScale = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[FontScaleSection]).Returns(configuredValue);
    return new FontScaleSetting(config.Object, interactive, fontScale);
  }

  [Fact]
  public void Value_WhenNotConfigured_FallsBackToDefaultPercent()
  {
    Make().Value.Should().Be("100%");
  }

  [Fact]
  public void Value_WhenConfigured_ReturnsPercent()
  {
    Make(configuredValue: "1.25").Value.Should().Be("125%");
  }

  [Fact]
  public void Value_AlwaysParsesConfigInInvariantCulture()
  {
    // A persisted value uses '.' as the decimal separator regardless of the current culture.
    var previous = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
      Make(configuredValue: "1.5").Value.Should().Be("150%");
    }
    finally
    {
      CultureInfo.CurrentCulture = previous;
    }
  }

  [Fact]
  public void Value_WhenConfiguredValueUnparseable_FallsBackToDefaultPercent()
  {
    Make(configuredValue: "not-a-number").Value.Should().Be("100%");
  }

  [Fact]
  public void Value_WhenConfiguredAboveMaximum_IsClampedToMaximum()
  {
    Make(configuredValue: "5.0").Value.Should().Be("200%");
  }

  [Fact]
  public void Value_WhenConfiguredBelowMinimum_IsClampedToMinimum()
  {
    Make(configuredValue: "0.1").Value.Should().Be("75%");
  }

  [Theory]
  [InlineData("150%")]
  [InlineData("150")]
  public void SetValue_AcceptsPercentWithOrWithoutSign_PersistsFactor(string entered)
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = entered;

    interactive.Verify(
      i => i.SetKey(FontScaleSection, (1.5).ToString(CultureInfo.InvariantCulture)),
      Times.Once);
  }

  [Fact]
  public void SetValue_AboveMaximum_PersistsClampedInvariantString()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = "300%";

    interactive.Verify(
      i => i.SetKey(
        FontScaleSection,
        FontScaleSetting.MaxFactor.ToString(CultureInfo.InvariantCulture)),
      Times.Once);
  }

  [Fact]
  public void SetValue_WhenUnparseable_DoesNotPersist()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = "abc";

    interactive.Verify(i => i.SetKey(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public void ResetToDefault_CallsRemoveKeyWithFontScaleSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(FontScaleSection), Times.Once);
  }
}
