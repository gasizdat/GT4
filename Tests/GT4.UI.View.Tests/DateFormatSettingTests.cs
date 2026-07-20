using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

/// <summary>
/// Covers <see cref="DateFormatSetting"/>, the single class backing both keyed date-format
/// settings (Full, Short) previously implemented as separate subclasses.
/// </summary>
public class DateFormatSettingTests
{
  private static DateFormatSetting MakeFull(
    string? configuredValue = null,
    Mock<IInteractiveConfiguration>? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c["DateFormatter.FullDateFormat"]).Returns(configuredValue);
    return new DateFormatSetting(
      config.Object,
      interactive?.Object,
      DateFormatKind.Full);
  }

  private static DateFormatSetting MakeShort(
    string? configuredValue = null,
    Mock<IInteractiveConfiguration>? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c["DateFormatter.ShortDateFormat"]).Returns(configuredValue);
    return new DateFormatSetting(
      config.Object,
      interactive?.Object,
      DateFormatKind.Short);
  }

  [Fact]
  public void Full_Value_WhenNotConfigured_ReturnsDefault()
  {
    MakeFull().Value.Should().Be("DD MM YYYY");
  }

  [Fact]
  public void Full_Value_WhenConfigured_ReturnsConfiguredValue()
  {
    MakeFull(configuredValue: "YYYY-MM-DD").Value.Should().Be("YYYY-MM-DD");
  }

  [Fact]
  public void Full_SetValue_WithInteractiveConfig_CallsSetKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeFull(interactive: interactive);

    setting.Value = "DD-MM-YYYY";

    interactive.Verify(i => i.SetKey("DateFormatter.FullDateFormat", "DD-MM-YYYY"), Times.Once);
  }

  [Fact]
  public void Full_SetValue_WithoutInteractiveConfig_DoesNotThrow()
  {
    var setting = MakeFull();
    var act = () => { setting.Value = "DD-MM-YYYY"; };
    act.Should().NotThrow();
  }

  [Fact]
  public void Full_ResetToDefault_WithInteractiveConfig_CallsRemoveKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeFull(interactive: interactive);

    setting.ResetToDefault();

    interactive.Verify(i => i.RemoveKey("DateFormatter.FullDateFormat"), Times.Once);
  }

  [Fact]
  public void Full_ResetToDefault_WithoutInteractiveConfig_DoesNotThrow()
  {
    var act = () => MakeFull().ResetToDefault();
    act.Should().NotThrow();
  }

  [Fact]
  public void Full_Group_ReturnsDateFormatter()
  {
    MakeFull().Group.Should().Be("DateFormatter");
  }

  [Fact]
  public void Full_Example_AppliesConfiguredValueAsFormat()
  {
    MakeFull(configuredValue: "no placeholders here").Example.Should().Be("no placeholders here");
  }

  [Fact]
  public void Short_Value_WhenNotConfigured_ReturnsDefault()
  {
    MakeShort().Value.Should().Be("MM YYYY");
  }

  [Fact]
  public void Short_Group_ReturnsDateFormatter()
  {
    MakeShort().Group.Should().Be("DateFormatter");
  }

  [Fact]
  public void Short_SetValue_WithInteractiveConfig_CallsSetKeyWithShortDateSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeShort(interactive: interactive);

    setting.Value = "MM-YYYY";

    interactive.Verify(i => i.SetKey("DateFormatter.ShortDateFormat", "MM-YYYY"), Times.Once);
  }

  [Fact]
  public void Short_ResetToDefault_CallsRemoveKeyWithShortDateSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeShort(interactive: interactive);

    setting.ResetToDefault();

    interactive.Verify(i => i.RemoveKey("DateFormatter.ShortDateFormat"), Times.Once);
  }

  [Fact]
  public void Short_Example_AppliesConfiguredValueAsFormat()
  {
    MakeShort(configuredValue: "no placeholders here").Example.Should().Be("no placeholders here");
  }
}
