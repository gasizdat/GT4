using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class FullDateFormatSettingTests
{
  private static FullDateFormatSetting Make(
    string? configuredValue = null,
    Mock<IInteractiveConfiguration>? interactive = null,
    Mock<IServiceProvider>? sp = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c["DateFormatter.FullDateFormat"]).Returns(configuredValue);
    return new FullDateFormatSetting(
      sp?.Object ?? new Mock<IServiceProvider>().Object,
      config.Object,
      interactive?.Object);
  }

  [Fact]
  public void Value_WhenNotConfigured_ReturnsDefault()
  {
    Make().Value.Should().Be("DD MM YYYY");
  }

  [Fact]
  public void Value_WhenConfigured_ReturnsConfiguredValue()
  {
    Make(configuredValue: "YYYY-MM-DD").Value.Should().Be("YYYY-MM-DD");
  }

  [Fact]
  public void SetValue_WithInteractiveConfig_CallsSetKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = Make(interactive: interactive);

    setting.Value = "DD-MM-YYYY";

    interactive.Verify(i => i.SetKey("DateFormatter.FullDateFormat", "DD-MM-YYYY"), Times.Once);
  }

  [Fact]
  public void SetValue_WithoutInteractiveConfig_DoesNotThrow()
  {
    var setting = Make();
    var act = () => { setting.Value = "DD-MM-YYYY"; };
    act.Should().NotThrow();
  }

  [Fact]
  public void ResetToDefault_WithInteractiveConfig_CallsRemoveKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = Make(interactive: interactive);

    setting.ResetToDefault();

    interactive.Verify(i => i.RemoveKey("DateFormatter.FullDateFormat"), Times.Once);
  }

  [Fact]
  public void ResetToDefault_WithoutInteractiveConfig_DoesNotThrow()
  {
    var act = () => Make().ResetToDefault();
    act.Should().NotThrow();
  }

  [Fact]
  public void Group_ReturnsDateFormatter()
  {
    Make().Group.Should().Be("DateFormatter");
  }

  [Fact]
  public void Example_CallsDateFormatterWithCurrentDate()
  {
    var formatter = new Mock<IDateFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<Date?>())).Returns("01 Jan 2025");

    var sp = new Mock<IServiceProvider>();
    sp.Setup(s => s.GetService(typeof(IDateFormatter))).Returns(formatter.Object);

    _ = Make(sp: sp).Example;

    formatter.Verify(f => f.ToString(It.Is<Date?>(d => d.HasValue && d.Value.Status == DateStatus.WellKnown)), Times.Once);
  }
}