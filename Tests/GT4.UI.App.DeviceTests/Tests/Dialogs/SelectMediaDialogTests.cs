using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Behaviors;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using Moq;
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

  // Issue #420: all three are foreign (own-media-first still separates them as a group), and all
  // three share the same owner, so a sort keyed off Owners would leave them in data-set order
  // instead of the order their file names show.
  [Fact]
  public async Task Foreign_items_sharing_an_owner_are_ordered_by_their_titles_not_the_owner_string()
  {
    var services = new TestServices();
    var ivan = new PersonInfo(1, default(Date), null, BiologicalSex.Male, [new Name(100, "Ivan", NameType.FirstName | NameType.MaleDeclension, null)], null);
    Data Attachment(int id, string fileName) =>
      new(id, GedcomPhotoResidue.EncodeAttachment([1, 2, 3], fileName), "application/pdf", DataCategory.PersonAttachment);
    services.Data.Setup(d => d.GetDataSetAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync([Attachment(50, "zebra.pdf"), Attachment(51, "apple.pdf"), Attachment(52, "mango.pdf")]);
    services.PersonManager.Setup(p => p.GetPersonInfosAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync([ivan]);
    services.PersonData.Setup(p => p.GetPersonIdsByDataAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<int, int[]> { [50] = [ivan.Id], [51] = [ivan.Id], [52] = [ivan.Id] });

    var dialog = await CreateDialogAsync(services);
    var items = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog.Items.ToArray()),
      items => items.Length == 3,
      timeoutMessage: "The dialog did not settle on 3 item(s); check the TestServices mock setup.");

    Assert.Equal([51, 52, 50], items.Select(item => item.Info.Id));
  }
}
