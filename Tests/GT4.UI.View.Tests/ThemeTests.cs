using FluentAssertions;
using GT4.UI.Utils.Settings;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.View.Tests;

// Covers the theme-resolution logic of Theme. Apply's write to UserAppTheme requires a running MAUI
// Application (Application.Current), which is null in the test host, so it is exercised only far
// enough to confirm it no-ops safely; Parse is fully observable here.
public class ThemeTests
{
  [Theory]
  [InlineData("Unspecified", AppTheme.Unspecified)]
  [InlineData("Light", AppTheme.Light)]
  [InlineData("Dark", AppTheme.Dark)]
  public void Parse_ANamedTheme_ResolvesToThatTheme(string persisted, AppTheme expected)
  {
    Theme.Parse(persisted).Should().Be(expected);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("System")]
  [InlineData("Sepia")]
  public void Parse_AnUnknownTheme_FallsBackToTheDefault(string? persisted)
  {
    Theme.Parse(persisted).Should().Be(Theme.Default);
  }

  // The picker persists nameof(AppTheme.X), so a rename of the enum member would silently reset every
  // user's stored choice to the default.
  [Fact]
  public void Default_IsTheThemeThatFollowsTheSystem()
  {
    Theme.Default.Should().Be(AppTheme.Unspecified);
  }

  [Fact]
  public void Apply_WithoutRunningApplication_DoesNotThrow()
  {
    var theme = new Theme();

    var apply = () => theme.Apply(nameof(AppTheme.Dark));

    apply.Should().NotThrow();
  }
}
