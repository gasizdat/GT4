using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class BackgroundAnimationSettingTests
{
  private const string BackgroundAnimationSection = "Appearance.BackgroundAnimation";

  private static BackgroundAnimationSetting Make(
    string? configuredValue = null,
    IInteractiveConfiguration? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[BackgroundAnimationSection]).Returns(configuredValue);
    return new BackgroundAnimationSetting(config.Object, interactive, null);
  }

  [Fact]
  public void Kind_IsBoolean()
  {
    Make().Kind.Should().Be(new SettingKind.Boolean());
  }

  [Fact]
  public void Value_WhenNotConfigured_FallsBackToAParseableTrue()
  {
    // The editor renders a Switch, which reads the value through bool.TryParse: the unconfigured
    // fallback has to parse, not merely look like a boolean.
    bool.TryParse(Make().Value, out var enabled).Should().BeTrue();
    enabled.Should().BeTrue();
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void SetValue_PersistsTheLiteralBoolToStringProduces(bool enabled)
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = enabled.ToString();

    interactive.Verify(i => i.SetKey(BackgroundAnimationSection, enabled ? "True" : "False"), Times.Once);
  }

  [Fact]
  public void ResetToDefault_CallsRemoveKeyWithBackgroundAnimationSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(BackgroundAnimationSection), Times.Once);
  }
}
