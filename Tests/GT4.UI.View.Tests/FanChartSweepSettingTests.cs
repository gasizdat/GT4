using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class FanChartSweepSettingTests
{
  private const string SweepSection = "FanChart.Sweep";

  private static FanChartSweepSetting Make(
    string? configuredValue = null,
    IInteractiveConfiguration? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[SweepSection]).Returns(configuredValue);
    return new FanChartSweepSetting(config.Object, interactive);
  }

  private static SettingKind.Option[] OptionsOf(ISettingEditor setting)
  {
    var choice = setting.Kind.Should().BeOfType<SettingKind.Choice>().Subject;
    return choice.Options;
  }

  [Fact]
  public void Kind_OffersAHalfThreeQuartersAndAWholeCircle()
  {
    var options = OptionsOf(Make());

    options.Select(o => o.Value).Should().Equal("180", "270", "360");
    options.Select(o => o.Label).Should().Equal("180°", "270°", "360°");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("90")]
  [InlineData("wide")]
  public void Value_WhenNotConfiguredOrUnknown_FallsBackToTheWholeCircle(string? configured)
  {
    Make(configured).Value.Should().Be("360");
  }

  [Theory]
  [InlineData("180")]
  [InlineData("270")]
  public void Value_WhenConfigured_ReadsBackTheConfiguredSweep(string configured)
  {
    Make(configured).Value.Should().Be(configured);
  }

  // Every Value the getter can return has to name an option, or a hand-edited config would throw
  // while SettingsPage builds the card.
  [Theory]
  [InlineData(null)]
  [InlineData("270")]
  [InlineData("wide")]
  public void Example_ShowsTheSelectedOptionsLabel(string? configured)
  {
    var setting = Make(configured);
    var selected = OptionsOf(setting).Single(o => o.Value == setting.Value);

    setting.Example.Should().Be(selected.Label);
  }

  [Fact]
  public void SetValue_PersistsTheChosenSweep()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = "180";

    interactive.Verify(i => i.SetKey(SweepSection, "180"), Times.Once);
  }

  [Fact]
  public void ResetToDefault_RemovesTheKeyWithoutPersistingAReplacement()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make("180", interactive.Object).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(SweepSection), Times.Once);
    interactive.Verify(i => i.SetKey(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }
}
