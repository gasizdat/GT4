using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes CreateOrUpdatePersonDialog's OnAddPersonNameAsync/OnEditPersonNameAsync seams: both push a
/// modal SelectNameDialog, so the test needs to await its own continuation rather than go through the
/// public DialogCommand, whose Execute is fire-and-forget.
/// </summary>
internal sealed class TestableCreateOrUpdatePersonDialog : CreateOrUpdatePersonDialog
{
  public new record class Factory(
    ICancellationTokenProvider CancellationTokenProvider,
    IBiologicalSexFormatter BiologicalSexFormatter,
    INameTypeFormatter NameTypeFormatter,
    INameFormatter NameFormatter,
    IDateFormatter DateFormatter,
    IComparer<PersonInfo> PersonInfoComparer,
    IAlertService AlertService,
    DataConverterResolver DataConverterFactory,
    SelectNameDialog.Factory SelectNameDialogFactory,
    SelectRelativesDialog.Factory SelectRelativesDialogFactory) 
    : CreateOrUpdatePersonDialog.Factory(
      CancellationTokenProvider,
      BiologicalSexFormatter,
      NameTypeFormatter,
      NameFormatter,
      DateFormatter,
      PersonInfoComparer,
      AlertService,
      DataConverterFactory,
      SelectNameDialogFactory,
      SelectRelativesDialogFactory)
  {
    public new TestableCreateOrUpdatePersonDialog Create(PersonFullInfo? person) =>
      new TestableCreateOrUpdatePersonDialog(this, person);
  }

  public TestableCreateOrUpdatePersonDialog(TestableCreateOrUpdatePersonDialog.Factory factory, PersonFullInfo? person)
    : base(factory, person)
  {
  }

  public Task InvokeAddPersonNameAsync() => OnAddPersonNameAsync();

  public Task InvokeEditPersonNameAsync(NameInfoItem name) => OnEditPersonNameAsync(name);
}
