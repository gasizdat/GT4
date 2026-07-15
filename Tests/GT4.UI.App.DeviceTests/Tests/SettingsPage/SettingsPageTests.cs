using GT4.UI.Pages;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// SettingsPage gathers every keyed ISettingEditor registration via
/// serviceProvider.GetKeyedServices(KeyedService.AnyKey) -- a "gather all" query, not the
/// one-dependency-at-a-time service-locator pattern the other pages moved off of (see
/// feedback_di_constructor_injection), so it keeps its raw IServiceProvider constructor and needs no
/// protected-seam subclass. All nine real ISettingEditor implementations are exercised as-is
/// (unmocked), since grouping/ordering real settings is the actual behavior being covered.
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

    Assert.Equal(9, editors.Length);
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
