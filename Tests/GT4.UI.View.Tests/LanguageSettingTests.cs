using FluentAssertions;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class LanguageSettingTests
{
  private const string LanguageSection = "Localization.Language";

  private static LanguageSetting Make(
    string? configuredValue = null,
    IInteractiveConfiguration? interactive = null)
  {
    var config = new Mock<IConfiguration>();
    config.SetupGet(c => c[LanguageSection]).Returns(configuredValue);
    return new LanguageSetting(config.Object, interactive);
  }

  [Fact]
  public void Value_WhenConfigured_ReturnsMatchingLanguage()
  {
    Make(configuredValue: "ru").Value.Should().Be(Language.RU);
  }

  [Fact]
  public void Value_WhenNotConfigured_FallsBackToCurrentLanguage()
  {
    Language.Current = Language.EN;

    Make().Value.Should().Be(Language.EN);
  }

  [Fact]
  public void Value_WhenConfiguredValueUnknown_FallsBackToCurrentLanguage()
  {
    Language.Current = Language.EN;

    Make(configuredValue: "zz").Value.Should().Be(Language.EN);
  }

  [Fact]
  public void SetValue_WithInteractiveConfig_CallsSetKeyWithLanguageCode()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).Value = Language.RU;

    interactive.Verify(i => i.SetKey(LanguageSection, "ru"), Times.Once);
  }

  [Fact]
  public void ResetToDefault_CallsRemoveKeyWithLanguageSection()
  {
    var interactive = new Mock<IInteractiveConfiguration>();

    Make(interactive: interactive.Object).ResetToDefault();

    interactive.Verify(i => i.RemoveKey(LanguageSection), Times.Once);
  }
}
