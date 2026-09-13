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

  // Asserts the behavior's own IsFocused against the live idiom rather than assuming Desktop: GitHub's
  // hosted windows-latest CI agent reports DeviceInfo.Idiom == Unknown, not Desktop (confirmed by a
  // throwaway diagnostic test), so a hardcoded True would fail there even though the gate is correct.
  // Reading no window attach needed either: without one, Loaded never fires, so the behavior never
  // runs its focus-and-reset cycle and IsFocused stays at whatever OnIdiom assigned it.
  [Fact]
  public async Task Dialog_appearing_focuses_the_owner_filter_entry_iff_desktop()
  {
    var dialog = await CreateDialogAsync(new TestServices());
    var ownerEntry = dialog.FindByName<Entry>("OwnerFilterEntry");

    var behavior = ownerEntry.Behaviors.OfType<FocusOnTrueBehavior>().Single();

    Assert.Equal(DeviceInfo.Idiom == DeviceIdiom.Desktop, behavior.IsFocused);
  }
}
