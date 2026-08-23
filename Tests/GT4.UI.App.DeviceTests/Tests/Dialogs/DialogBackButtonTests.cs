using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers the one rule every modal dialog shares: its way out completes the result task its caller is
/// parked on, and never dismisses the page itself -- the caller owns the push/await/pop protocol.
/// GedcomImportDialog is absent because it cancels a running import rather than returning a result,
/// and PhotoViewerDialog because it is not built on PageLayout at all.
/// </summary>
public class DialogBackButtonTests
{
  private sealed record Dialog(ContentPage Page, Task<object?> Result, object? Cancelled);

  public static TheoryData<string> Dialogs =>
  [
    nameof(CreateOrUpdateNameDialog),
    nameof(CreateOrUpdatePersonDialog),
    nameof(CreateOrUpdateProjectDialog),
    nameof(SelectDateDialog),
    nameof(SelectEncodingDialog),
    nameof(SelectMediaDialog),
    nameof(SelectNameDialog),
    nameof(SelectPersonDialog),
    nameof(SelectRelativesDialog),
  ];

  private static async Task<object?> BoxAsync<T>(Task<T> result) => await result;

  private static Dialog Create(string name, TestServices services)
  {
    var provider = services.Provider;
    var alertService = services.AlertService.Object;

    switch (name)
    {
      case nameof(CreateOrUpdateNameDialog):
        {
          var nameTypeFormatter = provider.GetRequiredService<INameTypeFormatter>();
          var tokenProvider = provider.GetRequiredService<ICancellationTokenProvider>();
          var converterResolver = provider.GetRequiredService<DataConverterResolver>();
          var dialog = new CreateOrUpdateNameDialog(
            NameType.FamilyName, nameTypeFormatter, alertService, tokenProvider, converterResolver);
          return new(dialog, BoxAsync(dialog.Info), null);
        }

      case nameof(CreateOrUpdatePersonDialog):
        {
          var factory = provider.GetRequiredService<CreateOrUpdatePersonDialog.Factory>();
          var dialog = factory.Create(null);
          return new(dialog, BoxAsync(dialog.Info), null);
        }

      case nameof(CreateOrUpdateProjectDialog):
        {
          var dialog = new CreateOrUpdateProjectDialog(null, alertService);
          var cancelled = new ProjectInfo(string.Empty, string.Empty, null, default!);
          return new(dialog, BoxAsync(dialog.ProjectInfo), cancelled);
        }

      case nameof(SelectDateDialog):
        {
          var dateFormatter = provider.GetRequiredService<IDateFormatter>();
          var dialog = new SelectDateDialog(null, dateFormatter, alertService);
          return new(dialog, BoxAsync(dialog.Info), null);
        }

      case nameof(SelectEncodingDialog):
        {
          var dialog = new SelectEncodingDialog("ANSI", alertService);
          return new(dialog, BoxAsync(dialog.Info), null);
        }

      case nameof(SelectMediaDialog):
        {
          var factory = provider.GetRequiredService<SelectMediaDialog.Factory>();
          var dialog = factory.Create([]);
          return new(dialog, BoxAsync(dialog.Info), null);
        }

      case nameof(SelectNameDialog):
        {
          var factory = provider.GetRequiredService<SelectNameDialog.Factory>();
          var dialog = factory.Create(BiologicalSex.Male, [NameType.FirstName]);
          return new(dialog, BoxAsync(dialog.Name), null);
        }

      case nameof(SelectPersonDialog):
        {
          var factory = provider.GetRequiredService<SelectPersonDialog.Factory>();
          var dialog = factory.Create();
          return new(dialog, BoxAsync(dialog.Info), null);
        }

      case nameof(SelectRelativesDialog):
        {
          var factory = provider.GetRequiredService<SelectRelativesDialog.Factory>();
          var dialog = factory.Create(null, []);
          return new(dialog, BoxAsync(dialog.Info), null);
        }

      default:
        throw new ArgumentOutOfRangeException(nameof(name), name, "No dialog is registered under that name.");
    }
  }

  private static async Task<Dialog> CreateAsync(string name)
  {
    var services = new TestServices();
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => Create(name, services));
  }

  [Theory]
  [MemberData(nameof(Dialogs))]
  public async Task Every_dialog_carries_its_title_where_the_back_button_can_sit_beside_it(string name)
  {
    var dialog = await CreateAsync(name);

    var layout = (PageLayout)dialog.Page.Content;

    Assert.True(layout.HasBackButton);
    Assert.True(layout.IsTitleVisible);
  }

  [Theory]
  [MemberData(nameof(Dialogs))]
  public async Task The_back_button_completes_the_result_the_caller_awaits(string name)
  {
    var dialog = await CreateAsync(name);
    var layout = (PageLayout)dialog.Page.Content;

    // A command-name mismatch leaves the result task pending, so bound the wait to fail, not hang.
    var backItem = layout.BackItem;
    await MainThread.InvokeOnMainThreadAsync(() => backItem.Command.Execute(backItem.CommandParameter));

    var result = await dialog.Result.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Equal(dialog.Cancelled, result);
  }

  // Android's default would dismiss the modal without completing the result task, leaving the caller
  // parked on its await forever -- and everything after it (the caller's own pop, refreshes) unrun.
  [Theory]
  [MemberData(nameof(Dialogs))]
  public async Task Hardware_back_cancels_the_dialog_rather_than_dismissing_it(string name)
  {
    var dialog = await CreateAsync(name);

    var handled = await MainThread.InvokeOnMainThreadAsync(() => dialog.Page.SendBackButtonPressed());

    Assert.True(handled);
    var result = await dialog.Result;
    Assert.Equal(dialog.Cancelled, result);
  }

  // The result task resumes the caller asynchronously, so hardware back can still land after the
  // footer button has already completed it.
  [Theory]
  [MemberData(nameof(Dialogs))]
  public async Task Cancelling_a_second_time_is_harmless(string name)
  {
    var dialog = await CreateAsync(name);
    await MainThread.InvokeOnMainThreadAsync(() => dialog.Page.SendBackButtonPressed());

    var exception = await Record.ExceptionAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => dialog.Page.SendBackButtonPressed()));

    Assert.Null(exception);
  }
}
