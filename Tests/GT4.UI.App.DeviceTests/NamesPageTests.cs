using GT4.Core.Project.Dto;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers NamesPage's dialog-free flows against a real MAUI runtime with a mocked Core.
/// PageCommand("AddName") and EditNameCommand push a modal CreateOrUpdateNameDialog via Navigation,
/// which a detached page cannot exercise; deferred until the page is hosted in a real Window.
/// </summary>
public class NamesPageTests
{
  [Fact]
  public async Task Ctor_resolves_dependencies_and_defaults()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var services = new TestServices();

    var page = await MainThread.InvokeOnMainThreadAsync(() => new TestableNamesPage(services.Provider));

    Assert.Equal(4, page.NameTypes.Count);
    Assert.Equal(3, page.BiologicalSexes.Count);
    Assert.Equal(page.NameTypes.First(), page.CurrentNameType);
    Assert.Equal(page.BiologicalSexes.First(), page.CurrentBiologicalSex);
    Assert.NotNull(page.EditNameCommand);
    Assert.NotNull(page.DeleteNameCommand);
    Assert.NotNull(page.PageCommand);
  }
}
