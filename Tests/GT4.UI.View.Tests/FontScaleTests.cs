using FluentAssertions;
using GT4.UI.Utils.Settings;
using Xunit;

namespace GT4.UI.View.Tests;

// Covers the factor-resolution logic of FontScale. The resource-rescaling branches require a running
// MAUI Application (Application.Current), which is null in the test host, so they are exercised only
// far enough to confirm they no-op safely; CurrentFactor is fully observable here.
public class FontScaleTests
{
  [Fact]
  public void CurrentFactor_BeforeApply_IsDefault()
  {
    new FontScale().CurrentFactor.Should().Be(FontScale.DefaultFactor);
  }

  [Theory]
  [InlineData(0.75)]
  [InlineData(1.0)]
  [InlineData(1.5)]
  [InlineData(2.0)]
  public void ApplyDouble_WithinRange_SetsFactorExactly(double factor)
  {
    var fontScale = new FontScale();

    fontScale.Apply(factor);

    fontScale.CurrentFactor.Should().Be(factor);
  }

  [Fact]
  public void ApplyDouble_AboveMaximum_ClampsToMaximum()
  {
    var fontScale = new FontScale();

    fontScale.Apply(3.0);

    fontScale.CurrentFactor.Should().Be(FontScale.MaxFactor);
  }

  [Theory]
  [InlineData(0.1)]
  [InlineData(-1.0)]
  public void ApplyDouble_BelowMinimum_ClampsToMinimum(double factor)
  {
    var fontScale = new FontScale();

    fontScale.Apply(factor);

    fontScale.CurrentFactor.Should().Be(FontScale.MinFactor);
  }

  [Fact]
  public void ApplyDouble_Null_FallsBackToDefault()
  {
    var fontScale = new FontScale();
    fontScale.Apply(1.5);

    fontScale.Apply((double?)null);

    fontScale.CurrentFactor.Should().Be(FontScale.DefaultFactor);
  }

  [Theory]
  [InlineData("150%", 1.5)]
  [InlineData("150", 1.5)]
  [InlineData("100%", 1.0)]
  [InlineData("75%", 0.75)]
  [InlineData("300%", FontScale.MaxFactor)]
  [InlineData("10%", FontScale.MinFactor)]
  public void ApplyPercent_ParsesAndClamps(string entered, double expected)
  {
    var fontScale = new FontScale();

    fontScale.Apply(entered);

    fontScale.CurrentFactor.Should().Be(expected);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("abc")]
  [InlineData("150.5%")] // non-integer percentages are rejected
  public void ApplyPercent_Unparseable_FallsBackToDefault(string? entered)
  {
    var fontScale = new FontScale();
    fontScale.Apply("150%");

    fontScale.Apply(entered);

    fontScale.CurrentFactor.Should().Be(FontScale.DefaultFactor);
  }

  [Fact]
  public void Initialize_WithoutRunningApplication_DoesNotThrowOrChangeFactor()
  {
    var fontScale = new FontScale();

    var initialize = () => fontScale.Initialize();

    initialize.Should().NotThrow();
    fontScale.CurrentFactor.Should().Be(FontScale.DefaultFactor);
  }
}
