using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Behaviors;
using GT4.UI.Dialogs;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SelectPersonDialog's auto-focus (issue #398): the search Entry gets keyboard focus as
/// soon as the dialog appears, the same FocusOnTrueBehavior wiring CreateOrUpdateNameDialog already
/// uses, gated to a desktop idiom via OnIdiom.
/// </summary>
public class SelectPersonDialogTests
{
  private static async Task<SelectPersonDialog> CreateDialogAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new SelectPersonDialog(
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.CurrentProjectProvider.Object,
      services.Provider.GetRequiredService<IComparer<PersonInfo>>(),
      services.AlertService.Object));
  }

  // Asserts the behavior's own IsFocused against the live idiom rather than assuming Desktop: GitHub's
  // hosted windows-latest CI agent reports DeviceInfo.Idiom == Unknown, not Desktop (confirmed by a
  // throwaway diagnostic test), so a hardcoded True would fail there even though the gate is correct.
  // Reading no window attach needed either: without one, Loaded never fires, so the behavior never
  // runs its focus-and-reset cycle and IsFocused stays at whatever OnIdiom assigned it.
  [Fact]
  public async Task Dialog_appearing_focuses_the_name_filter_entry_iff_desktop()
  {
    var dialog = await CreateDialogAsync(new TestServices());
    var nameEntry = dialog.FindByName<Entry>("NameFilterEntry");

    var behavior = nameEntry.Behaviors.OfType<FocusOnTrueBehavior>().Single();

    Assert.Equal(DeviceInfo.Idiom == DeviceIdiom.Desktop, behavior.IsFocused);
  }
}
