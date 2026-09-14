using Microsoft.Maui.Layouts;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Pins issue #394: a family card's person chips must land on the same column x-offsets as every
/// other card's, regardless of how long each card's own names are. ProjectPage.xaml drives this by
/// giving every chip the same FamilyPersonChipBasis (a FlexLayout.Basis fraction of the row), rather
/// than sizing chips from the widest name in their own card.
/// </summary>
public class FamilyPersonChipAlignmentTests
{
  private static async Task<(FlexLayout Row, Label First, Label Second)> CreateChipRowAsync(string firstText, string secondText)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    // The resource is stored as the raw OnIdiom<FlexBasis> wrapper; only its implicit conversion
    // operator resolves it to the current device's FlexBasis, same as XAML consumption does.
    var onIdiom = (OnIdiom<FlexBasis>)Application.Current!.Resources["FamilyPersonChipBasis"];
    FlexBasis basis = onIdiom;

    return await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var first = new Label { Text = firstText };
      var second = new Label { Text = secondText };
      FlexLayout.SetBasis(first, basis);
      FlexLayout.SetBasis(second, basis);

      var row = new FlexLayout
      {
        Direction = FlexDirection.Row,
        Wrap = FlexWrap.Wrap,
        WidthRequest = 600,
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
}
