using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class FontScaleSettingTests
{
  private const string FontScaleSection = "Appearance.FontScale";

  private static string Percent(double factor) => $"{(int)(factor * 100)}%";

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
  public void Value_WhenConfigured_ReturnsStoredPercent()
  {
    // The getter echoes the persisted percentage verbatim; clamping happens on write.
    Make(configuredValue: "150%").Value.Should().Be("150%");
  }

  [Theory]
  [InlineData("150%")]
  [InlineData("150")]
  public void SetValue_AcceptsPercentWithOrWithoutSign_PersistsPercent(string entered)
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object, fontScale: new FontScale()).Value = entered;

    interactive.Verify(i => i.SetKey(FontScaleSection, "150%"), Times.Once);
  }

  [Fact]
  public void SetValue_AppliesFactorToFontScale()
  {
    var fontScale = new FontScale();

    Make(interactive: new Mock<IInteractiveConfiguration>().Object, fontScale: fontScale).Value = "150%";

    fontScale.CurrentFactor.Should().Be(1.5);
  }

  [Fact]
  public void SetValue_AboveMaximum_PersistsClampedPercent()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object, fontScale: new FontScale()).Value = "300%";

    interactive.Verify(i => i.SetKey(FontScaleSection, Percent(FontScale.MaxFactor)), Times.Once);
  }

  [Fact]
  public void SetValue_BelowMinimum_PersistsClampedPercent()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object, fontScale: new FontScale()).Value = "10%";

    interactive.Verify(i => i.SetKey(FontScaleSection, Percent(FontScale.MinFactor)), Times.Once);
  }

  [Fact]
  public void SetValue_WhenUnparseable_PersistsDefaultPercent()
  {
    // An unparseable entry falls back to the unscaled baseline rather than persisting garbage.
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object, fontScale: new FontScale()).Value = "abc";

    interactive.Verify(i => i.SetKey(FontScaleSection, Percent(FontScale.DefaultFactor)), Times.Once);
  }

  [Fact]
  public void SetValue_WithoutFontScale_PersistsDefaultPercent()
  {
    // Guards the operator precedence in the persisted-string expression: with no FontScale to resolve
    // the factor, the fallback must yield the unscaled baseline ("100%"), not "1%".
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object, fontScale: null).Value = "150%";

    interactive.Verify(i => i.SetKey(FontScaleSection, Percent(FontScale.DefaultFactor)), Times.Once);
  }

  [Fact]
  public void ResetToDefault_CallsRemoveKeyWithFontScaleSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object, fontScale: new FontScale()).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(FontScaleSection), Times.Once);
  }
}
