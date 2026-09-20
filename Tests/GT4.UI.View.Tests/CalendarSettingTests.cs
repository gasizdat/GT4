using FluentAssertions;
using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
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
    IInteractiveConfiguration? interactive = null,
    string fullDateFormat = "no placeholders here")
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[CalendarSection]).Returns(configuredValue);

    var fullFormat = new Mock<ISettingEditor>();
    fullFormat.SetupGet(s => s.Value).Returns(fullDateFormat);

    return new CalendarSetting(config.Object, fullFormat.Object, interactive);
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

  // Year 1800 sits inside every non-Gregorian calendar's range (including French Republican's
  // narrow 1792-1806 window), so the preview must carry the selected calendar's own label, not the
  // out-of-range Gregorian fallback.
  [Theory]
  [InlineData(null, "no placeholders here")]
  [InlineData("Sepia", "no placeholders here")]
  [InlineData("Julian", "no placeholders here (Julian)")]
  [InlineData("Hebrew", "no placeholders here (Hebrew)")]
  [InlineData("FrenchRepublican", "no placeholders here (French Republican)")]
  public void Example_AppliesTheFormatAndLabelsNonGregorianCalendars(string? configured, string expected)
  {
    TestLanguage.Use(Language.EN);

    Make(configured).Example.Should().Be(expected);
  }

  // A no-placeholder format proves the suffix logic but never that the preview actually renders a
  // converted date -- 1800 lands in Hebrew year 5560 (Jan-Sep) or 5561 (late Sep-Dec) depending on
  // Rosh Hashanah's date that year, never anything else, so BeOneOf still discriminates a fallback
  // to Gregorian (which would read "1800") or a changed sentinel year.
  [Fact]
  public void Example_PreviewsTheDateInTheSelectedCalendar()
  {
    TestLanguage.Use(Language.EN);

    Make("Hebrew", fullDateFormat: "YYYY").Example.Should().BeOneOf("5560 (Hebrew)", "5561 (Hebrew)");
  }

  // Pinned against fixed (month, day) pairs rather than the system clock -- the bug this guards
  // against only ever showed up ~5 days a year, right before French Republican's Sept epoch, plus
  // Feb 29 (1800 isn't a leap year), so a clock-dependent test would rarely exercise either branch.
  [Theory]
  [InlineData(9, 20)] // lands in French Republican's own unmodeled complementary days every year
  [InlineData(2, 29)] // 1800 isn't a leap year; forcing it onto today's month/day would throw
  public void ResolveExampleDate_FallsBackToASafeDateWhenTodayIsNotRepresentable(int month, int day)
  {
    var today = Date.Create(1793, month, day, DateStatus.WellKnown);

    var resolved = CalendarSetting.ResolveExampleDate(today, nameof(DisplayCalendar.FrenchRepublican));

    (resolved.Year, resolved.Month, resolved.Day).Should().Be((1800, 1, 1));
  }

  [Fact]
  public void ResolveExampleDate_KeepsTodayWhenAlreadyRepresentable()
  {
    var today = Date.Create(1793, 6, 15, DateStatus.WellKnown);

    var resolved = CalendarSetting.ResolveExampleDate(today, nameof(DisplayCalendar.FrenchRepublican));

    (resolved.Year, resolved.Month, resolved.Day).Should().Be((1800, 6, 15));
  }

  [Fact]
  public void ResolveExampleDate_IgnoresRepresentabilityForOtherCalendars()
  {
    var today = Date.Create(1793, 9, 20, DateStatus.WellKnown);

    var resolved = CalendarSetting.ResolveExampleDate(today, nameof(DisplayCalendar.Gregorian));

    (resolved.Year, resolved.Month, resolved.Day).Should().Be((1800, 9, 20));
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
