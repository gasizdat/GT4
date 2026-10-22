using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// The card floats over a chart, so it paints a fill of its own, and that fill has to carry the
/// theme's ink for the name and dates.
/// </summary>
public class PersonCardViewTests
{
  private const double TextContrast = 4.5;

  private static PersonInfo P(int id, string firstName) =>
    new(id, Date.Create(1900, null, null, DateStatus.MonthUnknown), null, BiologicalSex.Male,
      [new Name(id * 100, firstName, NameType.FirstName | NameType.MaleDeclension, null)], null);

  [Fact]
  public async Task A_card_shows_only_while_it_has_a_person()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);

    var shown = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var card = new PersonCardView();
      var empty = card.IsVisible;
      card.Person = P(1, "Ivan");
      var filled = card.IsVisible;
      card.Person = null;
      return new[] { empty, filled, card.IsVisible };
    });

    Assert.Equal(new[] { false, true, false }, shown);
  }

  [Theory]
  [InlineData(AppTheme.Light)]
  [InlineData(AppTheme.Dark)]
  public async Task A_card_keeps_its_text_legible_over_its_own_fill(AppTheme theme)
  {
    var (fill, inks) = await ThemeContrast.UnderThemeAsync(theme, () =>
    {
      var card = new PersonCardView { Person = P(1, "Ivan") };
      var chrome = (Border)card.Content;
      var info = (PersonInfoView)chrome.Content!;
      var labels = ((Grid)info.Content).Children.OfType<Label>();
      return (chrome.BackgroundColor, labels.Select(label => label.TextColor).ToArray());
    });

    Assert.NotEmpty(inks);
    Assert.All(inks, ink =>
    {
      var ratio = ThemeContrast.Ratio(ink, fill);
      Assert.True(ratio >= TextContrast, $"{ink.ToArgbHex()} on {fill.ToArgbHex()} in {theme} is {ratio:F2}:1.");
    });
  }
}
