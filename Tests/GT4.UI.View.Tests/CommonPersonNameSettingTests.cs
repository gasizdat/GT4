using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class CommonPersonNameSettingTests
{
  private static CommonPersonNameSetting Make(
    string? configuredValue = null,
    Mock<IInteractiveConfiguration>? interactive = null,
    Mock<IServiceProvider>? sp = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c["NameFormatter.CommonPersonName"]).Returns(configuredValue);
    return new CommonPersonNameSetting(
      sp?.Object ?? new Mock<IServiceProvider>().Object,
      config.Object,
      interactive?.Object);
  }

  [Fact]
  public void Value_WhenNotConfigured_ReturnsDefault()
  {
    Make().Value.Should().Be("FF PP LL");
  }

  [Fact]
  public void Value_WhenConfigured_ReturnsConfiguredValue()
  {
    Make(configuredValue: "LL, FF PP").Value.Should().Be("LL, FF PP");
  }

  [Fact]
  public void SetValue_WithInteractiveConfig_CallsSetKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = Make(interactive: interactive);

    setting.Value = "LL FF";

    interactive.Verify(i => i.SetKey("NameFormatter.CommonPersonName", "LL FF"), Times.Once);
  }

  [Fact]
  public void SetValue_WithoutInteractiveConfig_DoesNotThrow()
  {
    var act = () => { Make().Value = "LL FF"; };
    act.Should().NotThrow();
  }

  [Fact]
  public void ResetToDefault_WithInteractiveConfig_CallsRemoveKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    Make(interactive: interactive).ResetToDefault();

    interactive.Verify(i => i.RemoveKey("NameFormatter.CommonPersonName"), Times.Once);
  }

  [Fact]
  public void ResetToDefault_WithoutInteractiveConfig_DoesNotThrow()
  {
    var act = () => Make().ResetToDefault();
    act.Should().NotThrow();
  }

  [Fact]
  public void Group_ReturnsNameFormatter()
  {
    Make().Group.Should().Be("NameFormatter");
  }

  [Fact]
  public void Example_CallsNameFormatterWithCommonPersonNameFormat()
  {
    var formatter = new Mock<INameFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<PersonInfo>(), It.IsAny<NameFormat>())).Returns("John Smith");

    var sp = new Mock<IServiceProvider>();
    sp.Setup(s => s.GetService(typeof(INameFormatter))).Returns(formatter.Object);

    _ = Make(sp: sp).Example;

    formatter.Verify(f => f.ToString(It.IsAny<PersonInfo>(), NameFormat.CommonPersonName), Times.Once);
  }
}