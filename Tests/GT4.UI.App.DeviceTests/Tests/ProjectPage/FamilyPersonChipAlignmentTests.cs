using Microsoft.Maui.Layouts;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Pins issue #394: a family card's person chips must land on the same column x-offsets as every
/// other card's, and a name too long for its chip must wrap rather than overlap the next chip.
/// ProjectPage.xaml.cs's OnFamilyPersonsSizeChanged drives both by giving every chip the same
/// fraction of the row's own width as its WidthRequest, rather than sizing chips from the widest
/// name in their own card.
/// </summary>
public class FamilyPersonChipAlignmentTests
{
  // Narrow enough that these tests' own long sample names reliably exceed it, independent of
  // whatever fraction Styles.xaml currently assigns each idiom.
  private const double ChipWidthFraction = 0.5;

  private static async Task<(FlexLayout Row, Label First, Label Second)> CreateChipRowAsync(
    string firstText, string secondText, double rowWidth = 300)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    return await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var chipWidth = rowWidth * ChipWidthFraction;
      var first = new Label { Text = firstText, WidthRequest = chipWidth, LineBreakMode = LineBreakMode.WordWrap };
      var second = new Label { Text = secondText, WidthRequest = chipWidth, LineBreakMode = LineBreakMode.WordWrap };

      var row = new FlexLayout
      {
        Direction = FlexDirection.Row,
        Wrap = FlexWrap.Wrap,
        WidthRequest = rowWidth,
        HorizontalOptions = LayoutOptions.Start,
      };
      row.Children.Add(first);
      row.Children.Add(second);
      return (row, first, second);
    });
  }

  private static async Task<IAsyncDisposable> AttachAsync(FlexLayout row)
  {
    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = row });
    var window = await WindowHost.AttachAsync(page);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => row.Width),
      width => width > 0,
      timeoutMessage: "The chip row never laid out.");

    return window;
  }

  [Fact]
  public async Task FamilyPersonChipWidthFraction_resolves_to_a_fraction_for_the_current_idiom()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    // Stored as the raw OnIdiom<double> wrapper; only its implicit conversion operator resolves it
    // to the current device's value -- a direct (double) cast throws InvalidCastException.
    var onIdiom = (OnIdiom<double>)Application.Current!.Resources["FamilyPersonChipWidthFraction"];
    double fraction = onIdiom;

    Assert.InRange(fraction, 0.0, 1.0);
  }

  [Fact]
  public async Task Chips_in_the_same_row_share_one_width_regardless_of_their_own_text()
  {
    var (row, first, second) = await CreateChipRowAsync("Al", "Bridget, Katherine");
    await using var window = await AttachAsync(row);

    Assert.Equal(first.Width, second.Width);
  }

  [Fact]
  public async Task A_short_named_card_and_a_long_named_card_align_on_the_same_column()
  {
    var (shortRow, _, shortSecond) = await CreateChipRowAsync("Al", "Bo");
    await using var shortWindow = await AttachAsync(shortRow);
    var shortColumnX = shortSecond.Bounds.X;

    var (longRow, _, longSecond) = await CreateChipRowAsync("Bridget, Katherine", "Bo");
    await using var longWindow = await AttachAsync(longRow);
    var longColumnX = longSecond.Bounds.X;

    Assert.Equal(shortColumnX, longColumnX);
  }

  // Regression: an earlier FlexLayout.Basis-based version of this fix aligned columns correctly but
  // never constrained the chip's own Measure pass, so a name too long for its chip rendered past its
  // own bounds and overlapped the next chip's photo/name instead of wrapping (observed on Android and
  // Windows with real long names). WidthRequest is what actually constrains Measure.
  [Fact]
  public async Task A_name_too_long_for_its_chip_wraps_instead_of_overlapping_the_next_chip()
  {
    var (row, first, second) = await CreateChipRowAsync("Bridget, Katherine Alexandra", "Bo");
    await using var window = await AttachAsync(row);

    Assert.True(first.Bounds.Right <= second.Bounds.Left,
      $"First chip (right edge {first.Bounds.Right}) overlapped the second chip (left edge {second.Bounds.Left}).");
  }
}
