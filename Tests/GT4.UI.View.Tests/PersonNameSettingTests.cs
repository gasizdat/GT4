using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

/// <summary>
/// Covers <see cref="PersonNameSetting"/>, the single class backing all four keyed person-name
/// format settings (CommonPersonName, FullPersonName, PersonInitials, ShortPersonName) previously
/// implemented as separate subclasses.
/// </summary>
public class PersonNameSettingTests
{
  private static PersonNameSetting Make(
    NameFormat nameFormat,
    string formatSection,
    string defaultFormat,
    string? configuredValue = null,
    Mock<IInteractiveConfiguration>? interactive = null,
    Mock<IServiceProvider>? sp = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[formatSection]).Returns(configuredValue);
    return new PersonNameSetting(
      sp?.Object ?? new Mock<IServiceProvider>().Object,
      config.Object,
      interactive?.Object,
      nameFormat);
  }

  [Theory]
  [InlineData(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL")]
  [InlineData(NameFormat.FullPersonName, "NameFormatter.FullPersonName", "FF PP LL (FN)")]
  [InlineData(NameFormat.PersonInitials, "NameFormatter.PersonInitialsSetting", "LL FF. PP.")]
  [InlineData(NameFormat.ShortPersonName, "NameFormatter.ShortPersonNameSetting", "FF PP")]
  public void Value_WhenNotConfigured_ReturnsDefault(NameFormat nameFormat, string section, string defaultFormat)
  {
    Make(nameFormat, section, defaultFormat).Value.Should().Be(defaultFormat);
  }

  [Fact]
  public void Value_WhenConfigured_ReturnsConfiguredValue()
  {
    Make(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL", configuredValue: "LL, FF PP")
      .Value.Should().Be("LL, FF PP");
  }

  [Theory]
  [InlineData(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL")]
  [InlineData(NameFormat.FullPersonName, "NameFormatter.FullPersonName", "FF PP LL (FN)")]
  [InlineData(NameFormat.PersonInitials, "NameFormatter.PersonInitialsSetting", "LL FF. PP.")]
  [InlineData(NameFormat.ShortPersonName, "NameFormatter.ShortPersonNameSetting", "FF PP")]
  public void SetValue_WithInteractiveConfig_CallsSetKeyWithCorrectSection(NameFormat nameFormat, string section, string defaultFormat)
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    var setting = Make(nameFormat, section, defaultFormat, interactive: interactive);

    setting.Value = "LL FF";

    interactive.Verify(i => i.SetKey(section, "LL FF"), Times.Once);
  }

  [Fact]
  public void SetValue_WithoutInteractiveConfig_DoesNotThrow()
  {
    var act = () => { Make(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL").Value = "LL FF"; };
    act.Should().NotThrow();
  }

  [Fact]
  public void ResetToDefault_WithInteractiveConfig_CallsRemoveKey()
  {
    var interactive = new Mock<IInteractiveConfiguration>();
    Make(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL", interactive: interactive).ResetToDefault();

    interactive.Verify(i => i.RemoveKey("NameFormatter.CommonPersonName"), Times.Once);
  }

  [Fact]
  public void ResetToDefault_WithoutInteractiveConfig_DoesNotThrow()
  {
    var act = () => Make(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL").ResetToDefault();
    act.Should().NotThrow();
  }

  [Fact]
  public void Group_ReturnsNameFormatter()
  {
    Make(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL").Group.Should().Be("NameFormatter");
  }

  [Theory]
  [InlineData(NameFormat.CommonPersonName, "NameFormatter.CommonPersonName", "FF PP LL")]
  [InlineData(NameFormat.FullPersonName, "NameFormatter.FullPersonName", "FF PP LL (FN)")]
  public void Example_CallsNameFormatterWithConfiguredNameFormat(NameFormat nameFormat, string section, string defaultFormat)
  {
    var formatter = new Mock<INameFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<PersonInfo>(), It.IsAny<NameFormat>())).Returns("John Smith");

    var sp = new Mock<IServiceProvider>();
    sp.Setup(s => s.GetService(typeof(INameFormatter))).Returns(formatter.Object);

    _ = Make(nameFormat, section, defaultFormat, sp: sp).Example;

    formatter.Verify(f => f.ToString(It.IsAny<PersonInfo>(), nameFormat), Times.Once);
  }
}
