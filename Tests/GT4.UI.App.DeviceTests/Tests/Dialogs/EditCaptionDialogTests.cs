using GT4.UI.Dialogs;
using Xunit;

namespace GT4.UI.DeviceTests;

public class EditCaptionDialogTests
{
  private static async Task<EditCaptionDialog> CreateDialogAsync(TestServices services, string? caption)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new EditCaptionDialog(caption, services.AlertService.Object));
  }

  [Fact]
  public async Task DialogCommand_returns_the_edited_caption()
  {
    var dialog = await CreateDialogAsync(new TestServices(), "Old caption");
    await MainThread.InvokeOnMainThreadAsync(() => dialog.Caption = "New caption");

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var result = await dialog.Info;

    Assert.Equal("New caption", result);
  }

  [Fact]
  public async Task DialogCommand_returns_null_when_the_caption_was_not_touched()
  {
    var dialog = await CreateDialogAsync(new TestServices(), "Old caption");

    await MainThread.InvokeOnMainThreadAsync(() => dialog.DialogCommand.Execute(null));
    var result = await dialog.Info;

    Assert.Null(result);
  }

  [Fact]
  public async Task CancelCommand_discardsTheEditAndReturnsNull()
  {
    var dialog = await CreateDialogAsync(new TestServices(), "Old caption");
    await MainThread.InvokeOnMainThreadAsync(() => dialog.Caption = "Typed but abandoned");

    await MainThread.InvokeOnMainThreadAsync(() => dialog.CancelCommand.Execute(null));
    var result = await dialog.Info;

    Assert.Null(result);
  }

  [Fact]
  public async Task BackButton_behavesLikeCancel()
  {
    var dialog = await CreateDialogAsync(new TestServices(), "Old caption");
    await MainThread.InvokeOnMainThreadAsync(() => dialog.Caption = "Typed but abandoned");

    var handled = await MainThread.InvokeOnMainThreadAsync(dialog.SendBackButtonPressed);
    var result = await dialog.Info;

    Assert.True(handled);
    Assert.Null(result);
  }
}
