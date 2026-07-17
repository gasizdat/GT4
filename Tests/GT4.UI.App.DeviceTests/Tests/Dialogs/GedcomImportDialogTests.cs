using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers GedcomImportDialog directly: its only service dependency is the IAlertService behind the
/// cancel command, so it's constructed straight from a TestServices mock.
/// The actual import (ProjectPage/ProjectListPage's OnImportGedcom) is out of scope -- FilePicker is
/// an external dependency -- but the cancellation UI this dialog owns is self-contained and testable.
/// </summary>
public class GedcomImportDialogTests
{
  private static async Task<TestableGedcomImportDialog> CreateDialogAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var services = new TestServices();
    return await MainThread.InvokeOnMainThreadAsync(() => new TestableGedcomImportDialog("My Tree", services.AlertService.Object));
  }

  [Fact]
  public async Task Ctor_starts_not_cancelling()
  {
    var dialog = await CreateDialogAsync();

    Assert.Equal("My Tree", dialog.ImportingProjectName);
    Assert.True(dialog.CanCancel);
    Assert.Equal(Resources.UIStrings.HintGedcomImportInProgress, dialog.StatusText);
    Assert.False(dialog.Token.IsCancellationRequested);
  }

  [Fact]
  public async Task DialogCommand_cancels_the_token_and_updates_status()
  {
    var dialog = await CreateDialogAsync();

    dialog.DialogCommand.Execute(null);

    Assert.False(dialog.CanCancel);
    Assert.Equal(Resources.UIStrings.HintGedcomImportCancelling, dialog.StatusText);
    Assert.True(dialog.Token.IsCancellationRequested);
  }

  [Fact]
  public async Task DialogCommand_is_idempotent()
  {
    var dialog = await CreateDialogAsync();

    dialog.DialogCommand.Execute(null);
    var exception = Record.Exception(() => dialog.DialogCommand.Execute(null));

    Assert.Null(exception);
  }

  [Fact]
  public async Task OnBackButtonPressed_cancels_and_blocks_the_default_dismissal()
  {
    var dialog = await CreateDialogAsync();

    var handled = dialog.InvokeOnBackButtonPressed();

    Assert.True(handled);
    Assert.True(dialog.Token.IsCancellationRequested);
  }
}
