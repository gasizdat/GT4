using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class NameSettingDefaultsTests
{
  private static Mock<IConfiguration> EmptyConfig() => new();

  [Fact]
  public void FullPersonNameSetting_DefaultValue()
  {
    var setting = new FullPersonNameSetting(
      new Mock<IServiceProvider>().Object, EmptyConfig().Object, interactiveConfiguration: null);
    setting.Value.Should().Be("FF PP LL (FN)");
  }

  [Fact]
  public void FullPersonNameSetting_Group_ReturnsNameFormatter()
  {
    var setting = new FullPersonNameSetting(
      new Mock<IServiceProvider>().Object, EmptyConfig().Object, interactiveConfiguration: null);
    setting.Group.Should().Be("NameFormatter");
  }

  [Fact]
  public void FullPersonNameSetting_SetValue_CallsSetKeyWithCorrectSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = new FullPersonNameSetting(
      new Mock<IServiceProvider>().Object, EmptyConfig().Object, interactive.Object);

    setting.Value = "LL FF";

    interactive.Verify(i => i.SetKey("NameFormatter.FullPersonName", "LL FF"), Times.Once);
  }

  [Fact]
  public void PersonInitialsSetting_DefaultValue()
  {
    var setting = new PersonInitialsSetting(
      new Mock<IServiceProvider>().Object, EmptyConfig().Object, interactiveConfiguration: null);
    setting.Value.Should().Be("LL FF. PP.");
  }

  [Fact]
  public void PersonInitialsSetting_SetValue_CallsSetKeyWithCorrectSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = new PersonInitialsSetting(
      new Mock<IServiceProvider>().Object, EmptyConfig().Object, interactive.Object);

    setting.Value = "FF. PP. LL";

    interactive.Verify(i => i.SetKey("NameFormatter.PersonInitialsSetting", "FF. PP. LL"), Times.Once);
  }

  [Fact]
  public void ShortPersonNameSetting_DefaultValue()
  {
    var setting = new ShortPersonNameSetting(
      new Mock<IServiceProvider>().Object, EmptyConfig().Object, interactiveConfiguration: null);
    setting.Value.Should().Be("FF PP");
  }

  [Fact]
  public void ShortPersonNameSetting_SetValue_CallsSetKeyWithCorrectSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = new ShortPersonNameSetting(
      new Mock<IServiceProvider>().Object, EmptyConfig().Object, interactive.Object);

    setting.Value = "FF PP";

    interactive.Verify(i => i.SetKey("NameFormatter.ShortPersonNameSetting", "FF PP"), Times.Once);
  }

  [Fact]
  public void FullPersonNameSetting_Example_CallsNameFormatterWithFullPersonNameFormat()
  {
    var formatter = new Mock<INameFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<PersonInfo>(), It.IsAny<NameFormat>())).Returns("Smith John");

    var sp = new Mock<IServiceProvider>();
    sp.Setup(s => s.GetService(typeof(INameFormatter))).Returns(formatter.Object);

    var setting = new FullPersonNameSetting(sp.Object, EmptyConfig().Object, interactiveConfiguration: null);
    _ = setting.Example;

    formatter.Verify(f => f.ToString(It.IsAny<PersonInfo>(), NameFormat.FullPersonName), Times.Once);
  }
}
