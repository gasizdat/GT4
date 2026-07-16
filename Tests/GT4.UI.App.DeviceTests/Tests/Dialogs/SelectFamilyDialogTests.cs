using GT4.Core.Project.Dto;
using GT4.UI.Dialogs;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class SelectFamilyDialogTests
{
  private static Name N(int id, string value) => new(id, value, NameType.FamilyName, null);

  private static async Task<SelectFamilyDialog> CreateDialogAsync(TestServices services, Name[] families)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new SelectFamilyDialog(families, services.Provider));
  }

  [Fact]
  public async Task Ctor_orders_families_and_defaults_to_cancel()
  {
    var services = new TestServices();
    var families = new[] { N(1, "Tolstoy"), N(2, "Aksakov"), N(3, "Pushkin") };

    var dialog = await CreateDialogAsync(services, families);

    Assert.Equal(["Aksakov", "Pushkin", "Tolstoy"], dialog.Families.Select(f => f.Value));
    Assert.Null(dialog.CurrentFamily);
    Assert.Equal(Resources.UIStrings.BtnNameCancel, dialog.DialogButtonName);
  }

  [Fact]
  public async Task FilterText_narrows_Families_via_wildcard_match()
  {
    var services = new TestServices();
    var families = new[] { N(1, "Tolstoy"), N(2, "Aksakov"), N(3, "Pushkin") };
    var dialog = await CreateDialogAsync(services, families);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.FilterText = "o");

    Assert.Equal(["Aksakov", "Tolstoy"], dialog.Families.Select(f => f.Value));
  }

  [Fact]
  public async Task CurrentFamily_changes_the_dialog_button_name()
  {
    var services = new TestServices();
    var families = new[] { N(1, "Tolstoy") };
    var dialog = await CreateDialogAsync(services, families);

    await MainThread.InvokeOnMainThreadAsync(() => dialog.CurrentFamily = families[0]);

    Assert.Equal(Resources.UIStrings.BtnNameOk, dialog.DialogButtonName);
  }

  [Fact]
  public async Task OnSelectFamily_completes_Family_with_the_current_selection()
  {
    var services = new TestServices();
    var families = new[] { N(1, "Tolstoy") };
    var dialog = await CreateDialogAsync(services, families);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.CurrentFamily = families[0]);

    await MainThread.InvokeOnMainThreadAsync(dialog.OnSelectFamily);

    var selected = await dialog.Family;
    Assert.Equal(families[0].Id, selected!.Id);
  }

  [Fact]
  public async Task OnAddFamilyAsync_creates_a_family_via_CreateOrUpdateNameDialog_and_selects_it()
  {
    var services = new TestServices();
    var families = new[] { N(1, "Tolstoy") };
    var addedFamily = N(10, "Ivanov");
    services.FamilyManager
      .Setup(f => f.AddFamilyAsync("Ivanov", "Ivan", "Ivanova", It.IsAny<CancellationToken>()))
      .ReturnsAsync(addedFamily);
    var dialog = await CreateDialogAsync(services, families);

    await using var window = await WindowHost.AttachAsync(dialog);
    var addTask = await MainThreadTask.StartAsync(dialog.OnAddFamilyAsync);
    var createDialog = await ModalDialogHarness.WaitForModalAsync<CreateOrUpdateNameDialog>(dialog);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      createDialog.GeneralName = "Ivanov";
      createDialog.MaleName = "Ivan";
      createDialog.FemaleName = "Ivanova";
      createDialog.OnCreateFamilyBtn(createDialog, EventArgs.Empty);
    });
    await addTask;

    services.FamilyManager.Verify(
      f => f.AddFamilyAsync("Ivanov", "Ivan", "Ivanova", It.IsAny<CancellationToken>()),
      Times.Once());
    Assert.Contains(dialog.Families, f => f.Id == addedFamily.Id);
    Assert.Equal(addedFamily.Id, dialog.CurrentFamily!.Id);
  }
}
