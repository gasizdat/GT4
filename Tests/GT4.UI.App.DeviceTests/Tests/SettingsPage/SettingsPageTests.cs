using GT4.Core.Utils;
using GT4.UI.Pages;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// SettingsPage gathers every keyed ISettingEditor registration via a SettingEditorsResolver
/// delegate (GT4.UI.Utils.Settings), itself backed by GetKeyedServices(KeyedService.AnyKey) --
/// a "gather all" query with no typed DI equivalent, registered once in
/// ServiceCollectionExtensions.AddUIUtils rather than resolved ad hoc. All ten real
/// ISettingEditor implementations are exercised as-is (unmocked), since grouping/ordering real
/// settings is the actual behavior being covered.
/// </summary>
public class SettingsPageTests
{
  private static async Task<SettingsPage> CreatePageAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<SettingsPage>());
  }

  [Fact]
  public async Task Ctor_resolves_every_registered_setting_editor()
  {
    var page = await CreatePageAsync(new TestServices());

    var editors = page.SettingEditors.ToArray();

    Assert.Equal(10, editors.Length);
  }

  [Fact]
  public async Task Every_registered_setting_declares_the_kind_its_Value_is_edited_as()
  {
    var page = await CreatePageAsync(new TestServices());

    var kinds = page.SettingEditors.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());

    Assert.Equal(
      new Dictionary<SettingKind, int>
      {
        // The eight format patterns, plus the font scale and the background animation.
        [SettingKind.Text] = 8,
        [SettingKind.BoundedNumeric] = 1,
        [SettingKind.Boolean] = 1,
      },
      kinds);
  }

  [Fact]
  public async Task Each_registered_setting_carries_the_metadata_its_kind_is_edited_through()
  {
    var page = await CreatePageAsync(new TestServices());

    var editors = page.SettingEditors.ToArray();

    var bounded = editors.Single(e => e.Kind == SettingKind.BoundedNumeric);
    Assert.IsType<NumericSettingMetadata>(bounded.Metadata);
    // No other kind has anything to say beyond its Value string yet.
    var rest = editors.Where(e => e.Kind != SettingKind.BoundedNumeric);
    Assert.All(rest, e => Assert.Null(e.Metadata));
  }

  [Fact]
  public async Task SettingEditors_are_grouped_with_displayname_ordering_within_each_group()
  {
    var page = await CreatePageAsync(new TestServices());

    var editors = page.SettingEditors.ToArray();

    // Grouped: once a group's run ends, that group must never reappear later in the sequence.
    var seenGroups = new HashSet<string>();
    string? currentGroup = null;
    foreach (var editor in editors)
    {
      if (editor.Group != currentGroup)
      {
        Assert.True(seenGroups.Add(editor.Group), $"Group '{editor.Group}' is not contiguous.");
        currentGroup = editor.Group;
      }
    }

    // Ordered by DisplayName within each group -- same default string comparison the page itself
    // uses (SettingsPage.SettingEditors: g.OrderBy(e => e.DisplayName)).
    foreach (var group in editors.GroupBy(e => e.Group))
    {
      var displayNames = group.Select(e => e.DisplayName).ToArray();
      Assert.Equal(displayNames.OrderBy(n => n).ToArray(), displayNames);
    }
  }
}
