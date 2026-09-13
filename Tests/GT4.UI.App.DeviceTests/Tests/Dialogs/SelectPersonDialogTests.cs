using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
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

  // The device-test runner is itself a desktop (WinUI) process, so this only pins the Desktop leg
  // of the OnIdiom gate; the touch-idiom leg (no auto-focus) is a deliberate manual-check gap, the
  // same shape as the other idiom-gated behavior documented in CLAUDE.md.
  [Fact]
  public async Task Dialog_appearing_focuses_the_name_filter_entry_on_desktop()
  {
    var dialog = await CreateDialogAsync(new TestServices());
    var nameEntry = dialog.FindByName<Entry>("NameFilterEntry");

    await using var window = await WindowHost.AttachAsync(dialog);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => nameEntry.IsFocused),
      focused => focused,
      timeoutMessage: "The dialog did not focus its name filter entry on a desktop idiom.");
  }
}
