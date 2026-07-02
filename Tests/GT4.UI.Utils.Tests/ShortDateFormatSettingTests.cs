using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class ShortDateFormatSettingTests
{
  private static ShortDateFormatSetting Make(
    string? configuredValue = null,
    Mock<IServiceProvider>? sp = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c["DateFormatter.ShortDateFormat"]).Returns(configuredValue);
    return new ShortDateFormatSetting(
      sp?.Object ?? new Mock<IServiceProvider>().Object,
      config.Object,
      interactiveConfiguration: null);
  }

  [Fact]
  public void Value_WhenNotConfigured_ReturnsDefault()
  {
    Make().Value.Should().Be("MM YYYY");
  }

  [Fact]
  public void Group_ReturnsDateFormatter()
  {
    Make().Group.Should().Be("DateFormatter");
  }

  [Fact]
  public void SetValue_WithInteractiveConfig_CallsSetKeyWithShortDateSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var config = new Mock<IConfiguration>();
    var setting = new ShortDateFormatSetting(
      new Mock<IServiceProvider>().Object,
      config.Object,
      interactive.Object);

    setting.Value = "MM-YYYY";

    interactive.Verify(i => i.SetKey("DateFormatter.ShortDateFormat", "MM-YYYY"), Times.Once);
  }

  [Fact]
  public void ResetToDefault_CallsRemoveKeyWithShortDateSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var config = new Mock<IConfiguration>();
    var setting = new ShortDateFormatSetting(
      new Mock<IServiceProvider>().Object,
      config.Object,
      interactive.Object);

    setting.ResetToDefault();

    interactive.Verify(i => i.RemoveKey("DateFormatter.ShortDateFormat"), Times.Once);
  }

  [Fact]
  public void Example_CallsDateFormatterWithDayUnknownDate()
  {
    var formatter = new Mock<IDateFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<Date?>())).Returns("Jan 2025");

    var sp = new Mock<IServiceProvider>();
    sp.Setup(s => s.GetService(typeof(IDateFormatter))).Returns(formatter.Object);

    _ = Make(sp: sp).Example;

    formatter.Verify(f => f.ToString(It.Is<Date?>(d => d.HasValue && d.Value.Status == DateStatus.DayUnknown)), Times.Once);
  }
}
