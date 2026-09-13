using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers SelectMediaDialog's auto-focus (issue #398): the owner filter Entry gets keyboard focus
/// as soon as the dialog appears, the same FocusOnTrueBehavior wiring CreateOrUpdateNameDialog
/// already uses, gated to a desktop idiom via OnIdiom.
/// </summary>
public class SelectMediaDialogTests
{
  private static async Task<SelectMediaDialog> CreateDialogAsync(TestServices services, int[]? ownMediaIds = null)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var factory = new SelectMediaDialog.Factory(
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.CurrentProjectProvider.Object,
      services.Provider.GetRequiredService<INameFormatter>(),
      services.AlertService.Object,
      services.Provider.GetRequiredService<DataConverterResolver>());
    return await MainThread.InvokeOnMainThreadAsync(() => factory.Create(ownMediaIds ?? []));
  }

  // The device-test runner is itself a desktop (WinUI) process, so this only pins the Desktop leg
  // of the OnIdiom gate; the touch-idiom leg (no auto-focus) is a deliberate manual-check gap, the
  // same shape as the other idiom-gated behavior documented in CLAUDE.md.
  [Fact]
  public async Task Dialog_appearing_focuses_the_owner_filter_entry_on_desktop()
  {
    var dialog = await CreateDialogAsync(new TestServices());
    var ownerEntry = dialog.FindByName<Entry>("OwnerFilterEntry");

    await using var window = await WindowHost.AttachAsync(dialog);

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => ownerEntry.IsFocused),
      focused => focused,
      timeoutMessage: "The dialog did not focus its owner filter entry on a desktop idiom.");
  }
}
