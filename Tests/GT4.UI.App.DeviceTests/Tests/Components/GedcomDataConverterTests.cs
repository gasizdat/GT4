using GT4.Core.Project.Dto;
using GT4.UI.Converters;
using GT4.UI.Resources;
using System.Globalization;
using System.Text;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers GedcomDataConverter's markdown rendering end to end, through the real GedcomReader/
/// GedcomNarrative parsing pipeline (feeding it raw GEDCOM residue text, not hand-built GedcomFact
/// trees, since Render/Append/TryFormatGeoLink/ParseCoordinate are all private -- ToObjectAsync is the
/// only entry point). Covers the label lookup/fallback, the bold-at-depth-0-only heading rule, the
/// MAP-with-LATI/LONG-becomes-a-Google-Maps-link special case (which, unlike the ordinary heading
/// rule, is never bolded even at depth 0), and ParseCoordinate's hemisphere-letter sign handling.
/// UIStrings.Culture is pinned to invariant (the neutral, English resx) for the duration of each
/// render: it defaults to null, which falls back to the test host's OS UI culture, and this repo is
/// developed on both English and Russian-locale machines -- DisableTestParallelization (see
/// AssemblyInfo.cs) is what makes mutating this static field safe across tests.
/// </summary>
public class GedcomDataConverterTests
{
  private static Data MakeResidue(string gedcomText) =>
    new(1, Encoding.UTF8.GetBytes(gedcomText), null, DataCategory.PersonGedcomTags);

  private static async Task<string?> RenderAsync(string gedcomText)
  {
    var previousCulture = UIStrings.Culture;
    UIStrings.Culture = CultureInfo.InvariantCulture;
    try
    {
      return (string?)await new GedcomDataConverter().ToObjectAsync(MakeResidue(gedcomText), CancellationToken.None);
    }
    finally
    {
      UIStrings.Culture = previousCulture;
    }
  }

  [Fact]
  public async Task ToObjectAsync_returns_null_for_a_null_residue()
  {
    var result = await new GedcomDataConverter().ToObjectAsync(null, CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task ToObjectAsync_returns_null_for_empty_residue_content()
  {
    var result = await new GedcomDataConverter().ToObjectAsync(
      new Data(1, [], null, DataCategory.PersonGedcomTags), CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task FromObjectAsync_always_throws_NotSupportedException()
  {
    await Assert.ThrowsAsync<NotSupportedException>(
      () => new GedcomDataConverter().FromObjectAsync(null, CancellationToken.None));
  }

  [Fact]
  public async Task Render_starts_with_the_localized_section_heading()
  {
    var markdown = await RenderAsync("0 OCCU Farmer\n");

    Assert.StartsWith("## Additional details\n\n", markdown);
  }

  [Fact]
  public async Task A_root_level_fact_renders_as_a_bold_labelled_bullet_with_its_value()
  {
    var markdown = await RenderAsync("0 OCCU Farmer\n");

    Assert.Contains("- **Occupation**: Farmer\n", markdown);
  }

  [Fact]
  public async Task A_fact_with_no_value_omits_the_colon_suffix()
  {
    var markdown = await RenderAsync("0 RELI\n");

    Assert.Contains("- **Religion**\n", markdown);
    Assert.DoesNotContain("Religion:", markdown);
  }

  [Fact]
  public async Task A_nested_fact_renders_indented_and_unbolded()
  {
    var markdown = await RenderAsync("0 EVEN Birth\n1 DATE 1 JAN 2000\n");

    Assert.Contains("- **Event**: Birth\n  - Date: 1 JAN 2000\n", markdown);
  }

  [Fact]
  public async Task An_unrecognized_tag_falls_back_to_the_raw_tag_as_its_label()
  {
    var markdown = await RenderAsync("0 ZZZFAKETAG value\n");

    Assert.Contains("- **ZZZFAKETAG**: value\n", markdown);
  }

  [Fact]
  public async Task A_MAP_with_valid_LATI_and_LONG_renders_as_an_unbolded_Google_Maps_link()
  {
    var markdown = await RenderAsync("0 MAP\n1 LATI N48.8979\n1 LONG E2.09592\n");

    // Unlike every other root-level fact, the MAP-as-geolink line is never bolded, even at depth 0.
    Assert.Contains(
      "- Coordinates: [48.8979, 2.09592](https://www.google.com/maps?q=48.8979,2.09592)\n",
      markdown);
    Assert.DoesNotContain("Latitude", markdown);
    Assert.DoesNotContain("Longitude", markdown);
  }

  [Fact]
  public async Task Southern_and_western_hemispheres_flip_the_sign()
  {
    var markdown = await RenderAsync("0 MAP\n1 LATI S48.8979\n1 LONG W2.09592\n");

    Assert.Contains("https://www.google.com/maps?q=-48.8979,-2.09592", markdown);
  }

  [Fact]
  public async Task A_coordinate_with_no_hemisphere_letter_is_read_as_already_signed()
  {
    var markdown = await RenderAsync("0 MAP\n1 LATI -48.8979\n1 LONG 2.09592\n");

    Assert.Contains("https://www.google.com/maps?q=-48.8979,2.09592", markdown);
  }

  [Fact]
  public async Task A_MAP_missing_LONG_falls_back_to_ordinary_bolded_nested_bullets()
  {
    var markdown = await RenderAsync("0 MAP\n1 LATI N48.8979\n");

    Assert.Contains("- **Coordinates**\n  - Latitude: N48.8979\n", markdown);
    Assert.DoesNotContain("google.com", markdown);
  }

  [Fact]
  public async Task A_MAP_with_an_unparsable_coordinate_falls_back_to_ordinary_nested_bullets()
  {
    var markdown = await RenderAsync("0 MAP\n1 LATI Nabc\n1 LONG E2.09592\n");

    Assert.DoesNotContain("google.com", markdown);
    Assert.Contains("Latitude: Nabc", markdown);
  }

  [Fact]
  public async Task A_MAP_with_a_valid_geo_link_still_nests_its_other_non_coordinate_children()
  {
    var markdown = await RenderAsync("0 MAP\n1 LATI N48.8979\n1 LONG E2.09592\n1 NOTE Home\n");

    Assert.Contains("google.com", markdown);
    Assert.Contains("  - Note: Home\n", markdown);
  }
}
