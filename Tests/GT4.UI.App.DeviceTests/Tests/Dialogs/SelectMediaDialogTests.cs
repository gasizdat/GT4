using GT4.Core.Utils;
using GT4.UI.Behaviors;
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

  // Asserts the behavior's own IsFocused rather than a real native Entry.IsFocused: the device-test
  // runner is a desktop (WinUI) process, so the OnIdiom gate resolves true here without needing a
  // window attach, but GitHub's hosted windows-latest CI agent cannot reliably grant a detached test
  // window real OS keyboard focus (observed as a CI-only flake) -- so real focus is not asserted.
  // The touch-idiom leg (no auto-focus) stays a deliberate manual-check gap, the same shape as the
  // other idiom-gated behavior documented in CLAUDE.md.
  [Fact]
  public async Task Dialog_appearing_focuses_the_owner_filter_entry_on_desktop()
  {
    var dialog = await CreateDialogAsync(new TestServices());
    var ownerEntry = dialog.FindByName<Entry>("OwnerFilterEntry");

    var behavior = ownerEntry.Behaviors.OfType<FocusOnTrueBehavior>().Single();

    Assert.True(behavior.IsFocused);
  }
}
