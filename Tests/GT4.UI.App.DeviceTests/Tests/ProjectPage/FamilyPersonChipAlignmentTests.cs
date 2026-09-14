using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using Microsoft.Maui.Layouts;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Pins issue #394: a family card's person chips must land on the same column x-offsets as every
/// other card's, and a name too long for its chip must clip there instead of bleeding into the next
/// chip. ProjectPage.xaml drives both from the row's own width -- FamilyPersonChipBasis groups chips
/// onto rows, WidthRequest (via ChipWidthFractionConverter) constrains each chip's own Measure --
/// rather than sizing chips from the widest name in their own card.
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

  private static async Task<IAsyncDisposable> AttachAsync(VisualElement content)
  {
    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = (View)content });
    var window = await WindowHost.AttachAsync(page);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => content.Width),
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

  private static Name N(int id, string value, NameType type) => new(id, value, type, null);
  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);
  private static PersonInfo P(int id, string firstName) =>
    new(id, UnknownDate, null, BiologicalSex.Male, [N(id * 100, firstName, NameType.FirstName)], null);

  private static Style TruncatedStyle => (Style)Application.Current!.Resources["FieldValueTruncated"];

  private static Label NameLabel(PersonInfoView chip) =>
    ((Grid)chip.Content).Children.OfType<Label>().Single(l => Grid.GetRow(l) == 0 && Grid.GetColumn(l) == 1);

  // A chip's own template wiring, mirroring ProjectPage.xaml's DataTemplate: Basis groups chips onto
  // rows but never reaches the name Label's Measure, so WidthRequest -- bound to the row's own Width
  // via ChipWidthFractionConverter, not set imperatively from a container-level SizeChanged -- is what
  // actually constrains it. A binding wired at template-creation time reaches every chip
  // SafeBindableLayout.Rebuild can produce, including ones inserted later for a grown family; an
  // imperative row-resize handler does not (see the regression test below).
  private static PersonInfoView CreateChip(FlexLayout row, FlexBasis basis, PersonInfo person)
  {
    var view = new PersonInfoView();
    view.SetValue(PersonInfoView.PersonProperty, person);
    view.SetValue(PersonInfoView.NameLabelStyleProperty, TruncatedStyle);
    view.SetValue(PersonInfoView.ShowDatesProperty, false);
    view.SetValue(PersonInfoView.ShowAgeProperty, false);
    FlexLayout.SetBasis(view, basis);
    view.SetBinding(VisualElement.WidthRequestProperty, new Binding(nameof(FlexLayout.Width), source: row, converter: new ChipWidthFractionConverter()));
    return view;
  }

  // Regression: FlexLayout.Basis sets a chip's own outer width but never reaches the name Label's
  // Measure pass, so under plain FieldValue (content-sized, HorizontalOptions=Start) with no
  // WidthRequest a name too long for its chip rendered past the chip's own bounds instead of wrapping
  // or clipping there -- observed on Android and Windows with real long names.
  [Fact]
  public async Task A_name_too_long_for_its_chip_is_clamped_to_the_chip_width_instead_of_bleeding()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var onIdiom = (OnIdiom<FlexBasis>)Application.Current!.Resources["FamilyPersonChipBasis"];
    FlexBasis basis = onIdiom;

    var (row, chip) = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var flexRow = new FlexLayout
      {
        Direction = FlexDirection.Row,
        Wrap = FlexWrap.Wrap,
        WidthRequest = 600,
        HorizontalOptions = LayoutOptions.Start,
      };
      var view = CreateChip(flexRow, basis, P(1, "Bridget Katherine Alexandra The Third Esquire"));
      flexRow.Children.Add(view);
      return (flexRow, view);
    });

    await using var window = await AttachAsync(row);

    // A sub-pixel rounding slack (not a real, visually-noticeable bleed) is fine here; the
    // full-natural-text-width bleed #394 regressed to is not.
    var nameLabel = NameLabel(chip);
    Assert.True(nameLabel.Width <= chip.Width + 1,
      $"Name label (width {nameLabel.Width}) exceeded its chip's own width ({chip.Width}).");
  }

  // Regression: SafeBindableLayout.Rebuild grows a recycled card's chip count by calling
  // InsertChildren for the new items, which creates them fresh from ItemTemplate but never re-runs
  // a container-level SizeChanged handler (the row's own width hasn't changed). A WidthRequest set
  // only by such a handler -- as ProjectPage.xaml.cs's OnFamilyPersonsSizeChanged used to -- would
  // leave a newly-inserted chip unconstrained and bleeding; wiring the binding into ItemTemplate
  // itself (CreateChip above) reaches it regardless of when or why it was created.
  [Fact]
  public async Task A_chip_inserted_into_an_already_laid_out_row_is_still_clamped()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var onIdiom = (OnIdiom<FlexBasis>)Application.Current!.Resources["FamilyPersonChipBasis"];
    FlexBasis basis = onIdiom;

    var row = await MainThread.InvokeOnMainThreadAsync(() => new FlexLayout
    {
      Direction = FlexDirection.Row,
      Wrap = FlexWrap.Wrap,
      WidthRequest = 600,
      HorizontalOptions = LayoutOptions.Start,
    });
    await using var window = await AttachAsync(row);

    var chip = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var view = CreateChip(row, basis, P(1, "Bridget Katherine Alexandra The Third Esquire"));
      row.Children.Add(view);
      return view;
    });

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => chip.Width),
      width => width > 0,
      timeoutMessage: "The inserted chip never laid out.");

    var nameLabel = NameLabel(chip);
    Assert.True(nameLabel.Width <= chip.Width + 1,
      $"Name label (width {nameLabel.Width}) exceeded its chip's own width ({chip.Width}).");
  }
}
