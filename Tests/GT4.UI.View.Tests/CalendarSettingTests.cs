using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Globalization;
using Xunit;

namespace GT4.UI.View.Tests;

public class CalendarSettingTests
{
  private const string CalendarSection = "DateFormatter.Calendar";

  private static CalendarSetting Make(
    string? configuredValue = null,
    IInteractiveConfiguration? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[CalendarSection]).Returns(configuredValue);
    return new CalendarSetting(config.Object, interactive);
  }

  private static SettingKind.Option[] OptionsOf(ISettingEditor setting)
  {
    var choice = setting.Kind.Should().BeOfType<SettingKind.Choice>().Subject;
    return choice.Options;
  }

  private static SettingKind.Option[] OptionsIn(ISettingEditor setting, string language)
  {
    var culture = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = new CultureInfo(language);
    try
    {
      return OptionsOf(setting);
    }
    finally
    {
      CultureInfo.CurrentUICulture = culture;
    }
  }

  [Fact]
  public void Kind_OffersEveryDisplayCalendar()
  {
    var values = OptionsOf(Make()).Select(o => o.Value);

    values.Should().Equal(
      nameof(DisplayCalendar.Gregorian),
      nameof(DisplayCalendar.Julian),
      nameof(DisplayCalendar.Hebrew),
      nameof(DisplayCalendar.FrenchRepublican));
  }

  // Labels are read from UIStrings on every Kind access rather than cached once, so switching the UI
  // language relabels the picker without the setting being rebuilt.
  [Fact]
  public void Kind_RelabelsItsOptionsWhenTheUILanguageChanges()
  {
    var setting = Make();
    var english = OptionsIn(setting, Language.EN.Code).Select(o => o.Label);
    var german = OptionsIn(setting, Language.DE.Code).Select(o => o.Label);

    german.Should().NotEqual(english);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("Sepia")]
  public void Value_WhenNotConfiguredOrUnknown_FallsBackToGregorian(string? configured)
  {
    Make(configured).Value.Should().Be(nameof(DisplayCalendar.Gregorian));
  }

  [Theory]
  [InlineData("Julian")]
  [InlineData("Hebrew")]
  [InlineData("FrenchRepublican")]
  public void Value_WhenConfigured_ReadsBackTheConfiguredCalendar(string configured)
  {
    Make(configured).Value.Should().Be(configured);
  }

  // Example feeds the card's preview label, and every Value the getter can return has to name an
  // option -- otherwise a hand-edited config would throw while the page builds.
  [Theory]
  [InlineData(null)]
  [InlineData("Hebrew")]
  [InlineData("Sepia")]
  public void Example_ShowsTheSelectedOptionsLabel(string? configured)
  {
    var setting = Make(configured);
    var selected = OptionsOf(setting).Single(o => o.Value == setting.Value);

    setting.Example.Should().Be(selected.Label);
  }

  [Fact]
  public void SetValue_PersistsTheChosenCalendar()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = nameof(DisplayCalendar.Hebrew);

    interactive.Verify(i => i.SetKey(CalendarSection, nameof(DisplayCalendar.Hebrew)), Times.Once);
  }

  [Fact]
  public void ResetToDefault_RemovesTheKeyWithoutPersistingAReplacement()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make("Hebrew", interactive.Object).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(CalendarSection), Times.Once);
    interactive.Verify(i => i.SetKey(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }
}
