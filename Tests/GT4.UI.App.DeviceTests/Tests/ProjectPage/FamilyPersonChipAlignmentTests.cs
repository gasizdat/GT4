using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Pins issue #394 against the real page: a family card's person chips must land on the same column
/// x-offsets as every other card's, and a name too long for its chip must wrap rather than overlap
/// the next chip. ProjectPage.xaml.cs's OnFamilyPersonsSizeChanged drives both by giving every chip
/// the same fraction of the row's own width as both its FlexLayout.Basis and its WidthRequest,
/// rather than sizing chips from the widest name in their own card.
///
/// Goes through the real ProjectPage/CollectionView/SafeBindableLayout pipeline via
/// TestableProjectPage.LastPersonChipsFlexLayout, not a hand-built FlexLayout: neither a bare Label
/// nor a FlexLayout sized before its first layout pass (rather than reactively, from SizeChanged,
/// the way OnFamilyPersonsSizeChanged actually runs) reproduced the bug this pins.
/// </summary>
public class FamilyPersonChipAlignmentTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);
  private static FamilyInfo FI(Name name) => new(name, null);
  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);
  private static PersonInfo P(int id, string firstName) =>
    new(id, UnknownDate, null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName)], null);
  private static PersonInfo InFamily(PersonInfo person, Name family) => person with { Names = [.. person.Names, family] };

  private static async Task<TestableProjectPage> CreatePageWithFamilyAsync(params string[] names)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    // Pin the fraction to 0.5 (Phone's value) regardless of the test host's own idiom: at Desktop's
    // narrower 0.2 (five columns), plenty of slack around WidthRequest hides this regression even
    // with the fix disabled -- it only reproduces reliably at the widest chips ship with (#394).
    await MainThread.InvokeOnMainThreadAsync(() =>
      Application.Current!.Resources["FamilyPersonChipWidthFraction"] =
        new OnIdiom<double> { Phone = 0.5, Tablet = 0.5, Desktop = 0.5, Default = 0.5 });

    var services = new TestServices();
    var family = N(1, "Family", NameType.FamilyName);
    services.FamilyManager.Setup(f => f.GetFamiliesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([FI(family)]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(true, It.IsAny<CancellationToken>()))
      .ReturnsAsync([.. names.Select((n, i) => InFamily(P(i + 1, n), family))]);

    var page = await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<TestableProjectPage>());
    await page.WaitForFamiliesAsync();
    return page;
  }

  private static async Task<IAsyncDisposable> AttachAndWaitForChipsAsync(TestableProjectPage page)
  {
    var window = await WindowHost.AttachAsync(page);
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => page.LastPersonChipsFlexLayout),
      flex => flex is not null,
      timeoutMessage: "The family card's person chips never resized.");
    return window;
  }

  private static PersonInfoView[] Chips(TestableProjectPage page) =>
    [.. page.LastPersonChipsFlexLayout!.Children.OfType<PersonInfoView>()];

  private static PersonInfoView ChipNamed(TestableProjectPage page, string name) =>
    Chips(page).Single(chip => ((PersonInfo)chip.GetValue(PersonInfoView.PersonProperty)).Names[0].Value == name);

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

  // Regression: with FlexLayout.Basis left at its Auto default (only WidthRequest applied
  // reactively), every chip landed on its own row regardless of how many would actually fit.
  [Fact]
  public async Task Two_chips_that_fit_the_row_share_one_line_instead_of_one_per_line()
  {
    var page = await CreatePageWithFamilyAsync("Al", "Bo");
    await using var window = await AttachAndWaitForChipsAsync(page);

    var chips = Chips(page);
    Assert.Equal(2, chips.Length);
    Assert.Equal(chips[0].Bounds.Y, chips[1].Bounds.Y);
  }

  [Fact]
  public async Task Chips_in_the_same_row_share_one_width_regardless_of_their_own_text()
  {
    var page = await CreatePageWithFamilyAsync("Al", "Bridget, Katherine");
    await using var window = await AttachAndWaitForChipsAsync(page);

    var chips = Chips(page);
    Assert.Equal(chips[0].Width, chips[1].Width);
  }

  [Fact]
  public async Task A_short_named_card_and_a_long_named_card_align_on_the_same_column()
  {
    // "Zoe" sorts after either first name, so it stays the second (rightmost) chip in both cards --
    // isolating the column x-offset from the DB's alphabetical ordering of who occupies it.
    var shortPage = await CreatePageWithFamilyAsync("Al", "Zoe");
    await using var shortWindow = await AttachAndWaitForChipsAsync(shortPage);
    var shortColumnX = ChipNamed(shortPage, "Zoe").Bounds.X;

    var longPage = await CreatePageWithFamilyAsync("Bridget, Katherine", "Zoe");
    await using var longWindow = await AttachAndWaitForChipsAsync(longPage);
    var longColumnX = ChipNamed(longPage, "Zoe").Bounds.X;

    Assert.Equal(shortColumnX, longColumnX);
  }

  // Regression: an earlier FlexLayout.Basis-only version of this fix (before WidthRequest was added
  // back) aligned columns correctly but never constrained the chip's own Measure pass, so a name too
  // long for its chip rendered past its own bounds and overlapped the next chip's photo/name instead
  // of wrapping (observed on Android and Windows with real long names).
  [Fact]
  public async Task A_name_too_long_for_its_chip_wraps_instead_of_overlapping_the_next_chip()
  {
    var page = await CreatePageWithFamilyAsync("Bridget Katherine Alexandra The Third Esquire", "Bo");
    await using var window = await AttachAndWaitForChipsAsync(page);

    var longChip = ChipNamed(page, "Bridget Katherine Alexandra The Third Esquire");
    var shortChip = ChipNamed(page, "Bo");

    Assert.Equal(longChip.Bounds.Y, shortChip.Bounds.Y);
    var (left, right) = longChip.Bounds.X < shortChip.Bounds.X ? (longChip, shortChip) : (shortChip, longChip);
    // A sub-pixel rounding slack (not a real, visually-noticeable overlap) is fine here; the
    // hundred-plus-pixel bleed #394 regressed to is not.
    Assert.True(left.Bounds.Right <= right.Bounds.Left + 1,
      $"Left chip (right edge {left.Bounds.Right}) overlapped the right chip (left edge {right.Bounds.Left}).");
  }
}
