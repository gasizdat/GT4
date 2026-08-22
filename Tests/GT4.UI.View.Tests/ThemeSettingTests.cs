using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.ApplicationModel;
using Moq;
using System.Globalization;
using Xunit;

namespace GT4.UI.View.Tests;

public class ThemeSettingTests
{
  private const string ThemeSection = "Appearance.Theme";

  private static ThemeSetting Make(
    string? configuredValue = null,
    IInteractiveConfiguration? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[ThemeSection]).Returns(configuredValue);
    return new ThemeSetting(config.Object, interactive, null);
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

  public static TheoryData<string> Languages => new(Language.Languages.Select(l => l.Code));

  [Fact]
  public void Kind_OffersSystemLightAndDark()
  {
    var values = OptionsOf(Make()).Select(o => o.Value);

    values.Should().Equal(nameof(AppTheme.Unspecified), nameof(AppTheme.Light), nameof(AppTheme.Dark));
  }

  // Value is the persisted string, not display text: the picker carries a separate label so that
  // "Unspecified" never reaches the user. The English labels of the other two options legitimately
  // read the same as their persisted values, which is why only the label text itself is constrained.
  [Theory]
  [MemberData(nameof(Languages))]
  public void Kind_NeverLabelsAnOptionWithUnspecified(string language)
  {
    var options = OptionsIn(Make(), language);

    options.Should().OnlyContain(o => o.Label.Length > 0 && o.Label != nameof(AppTheme.Unspecified));
  }

  // The labels are read from UIStrings on every Kind access rather than cached once, so switching the
  // UI language relabels the picker without the setting being rebuilt.
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
  public void Value_WhenNotConfiguredOrUnknown_FallsBackToTheDefault(string? configured)
  {
    Make(configured).Value.Should().Be(Theme.Default.ToString());
  }

  [Theory]
  [InlineData("Light")]
  [InlineData("Dark")]
  public void Value_WhenConfigured_ReadsBackTheConfiguredTheme(string configured)
  {
    Make(configured).Value.Should().Be(configured);
  }

  // Example feeds the card's preview label, and every Value the getter can return has to name an
  // option -- otherwise a hand-edited config would throw while the page builds.
  [Theory]
  [InlineData(null)]
  [InlineData("Dark")]
  [InlineData("Sepia")]
  public void Example_ShowsTheSelectedOptionsLabel(string? configured)
  {
    var setting = Make(configured);
    var selected = OptionsOf(setting).Single(o => o.Value == setting.Value);

    setting.Example.Should().Be(selected.Label);
  }

  [Fact]
  public void SetValue_PersistsTheChosenTheme()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = nameof(AppTheme.Dark);

    interactive.Verify(i => i.SetKey(ThemeSection, nameof(AppTheme.Dark)), Times.Once);
  }

  [Fact]
  public void ResetToDefault_RemovesTheKeyWithoutPersistingAReplacement()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make("Dark", interactive.Object).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(ThemeSection), Times.Once);
    interactive.Verify(i => i.SetKey(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }
}
