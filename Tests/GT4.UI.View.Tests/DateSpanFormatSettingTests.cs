using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

/// <summary>
/// Covers <see cref="DateSpanFormatSetting"/>, the single class backing both keyed date-span-format
/// settings (Full, Short), mirroring <see cref="DateFormatSetting"/>'s pattern.
/// </summary>
public class DateSpanFormatSettingTests
{
  private static DateSpanFormatSetting MakeFull(
    string? configuredValue = null,
    Mock<IInteractiveConfiguration>? interactive = null,
    Mock<IServiceProvider>? sp = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c["DateSpanFormatter.FullDateSpanFormat"]).Returns(configuredValue);
    return new DateSpanFormatSetting(
      sp?.Object ?? new Mock<IServiceProvider>().Object,
      config.Object,
      interactive?.Object,
      DateSpanFormatKind.Full);
  }

  private static DateSpanFormatSetting MakeShort(
    string? configuredValue = null,
    Mock<IInteractiveConfiguration>? interactive = null,
    Mock<IServiceProvider>? sp = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c["DateSpanFormatter.ShortDateSpanFormat"]).Returns(configuredValue);
    return new DateSpanFormatSetting(
      sp?.Object ?? new Mock<IServiceProvider>().Object,
      config.Object,
      interactive?.Object,
      DateSpanFormatKind.Short);
  }

  [Fact]
  public void Full_Value_WhenNotConfigured_ReturnsDefault()
  {
    MakeFull().Value.Should().Be("YEARS MONTHS DAYS");
  }

  [Fact]
  public void Full_Value_WhenConfigured_ReturnsConfiguredValue()
  {
    MakeFull(configuredValue: "DAYS, MONTHS, YEARS").Value.Should().Be("DAYS, MONTHS, YEARS");
  }

  [Fact]
  public void Full_SetValue_WithInteractiveConfig_CallsSetKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeFull(interactive: interactive);

    setting.Value = "YEARS, MONTHS";

    interactive.Verify(i => i.SetKey("DateSpanFormatter.FullDateSpanFormat", "YEARS, MONTHS"), Times.Once);
  }

  [Fact]
  public void Full_SetValue_WithoutInteractiveConfig_DoesNotThrow()
  {
    var setting = MakeFull();
    var act = () => { setting.Value = "YEARS, MONTHS"; };
    act.Should().NotThrow();
  }

  [Fact]
  public void Full_ResetToDefault_WithInteractiveConfig_CallsRemoveKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeFull(interactive: interactive);

    setting.ResetToDefault();

    interactive.Verify(i => i.RemoveKey("DateSpanFormatter.FullDateSpanFormat"), Times.Once);
  }

  [Fact]
  public void Full_ResetToDefault_WithoutInteractiveConfig_DoesNotThrow()
  {
    var act = () => MakeFull().ResetToDefault();
    act.Should().NotThrow();
  }

  [Fact]
  public void Full_Group_ReturnsDateSpanFormatter()
  {
    MakeFull().Group.Should().Be("DateSpanFormatter");
  }

  [Fact]
  public void Full_Example_CallsDateSpanFormatterWithWellKnownSpan()
  {
    var formatter = new Mock<IDateSpanFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<DateSpan?>())).Returns("25 years 3 months 15 days");

    var sp = new Mock<IServiceProvider>();
    sp.Setup(s => s.GetService(typeof(IDateSpanFormatter))).Returns(formatter.Object);

    _ = MakeFull(sp: sp).Example;

    formatter.Verify(f => f.ToString(It.Is<DateSpan?>(d => d.HasValue && d.Value.Status == DateStatus.WellKnown)), Times.Once);
  }

  [Fact]
  public void Short_Value_WhenNotConfigured_ReturnsDefault()
  {
    MakeShort().Value.Should().Be("YEARS MONTHS");
  }

  [Fact]
  public void Short_Group_ReturnsDateSpanFormatter()
  {
    MakeShort().Group.Should().Be("DateSpanFormatter");
  }

  [Fact]
  public void Short_SetValue_WithInteractiveConfig_CallsSetKeyWithShortDateSpanSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeShort(interactive: interactive);

    setting.Value = "MONTHS of YEARS";

    interactive.Verify(i => i.SetKey("DateSpanFormatter.ShortDateSpanFormat", "MONTHS of YEARS"), Times.Once);
  }

  [Fact]
  public void Short_ResetToDefault_CallsRemoveKeyWithShortDateSpanSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = MakeShort(interactive: interactive);

    setting.ResetToDefault();

    interactive.Verify(i => i.RemoveKey("DateSpanFormatter.ShortDateSpanFormat"), Times.Once);
  }

  [Fact]
  public void Short_Example_CallsDateSpanFormatterWithDayUnknownSpan()
  {
    var formatter = new Mock<IDateSpanFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<DateSpan?>())).Returns("5 years 6 months");

    var sp = new Mock<IServiceProvider>();
    sp.Setup(s => s.GetService(typeof(IDateSpanFormatter))).Returns(formatter.Object);

    _ = MakeShort(sp: sp).Example;

    formatter.Verify(f => f.ToString(It.Is<DateSpan?>(d => d.HasValue && d.Value.Status == DateStatus.DayUnknown)), Times.Once);
  }
}
