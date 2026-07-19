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
  public TestableCreateOrUpdatePersonDialog(
    PersonFullInfo? person,
    ICancellationTokenProvider cancellationTokenProvider,
    IBiologicalSexFormatter biologicalSexFormatter,
    INameTypeFormatter nameTypeFormatter,
    INameFormatter nameFormatter,
    IDateFormatter dateFormatter,
    IComparer<PersonInfo> personInfoComparer,
    IAlertService alertService,
    Func<DataCategory, IDataConverter> dataConverterFactory,
    Func<BiologicalSex, NameType[], SelectNameDialog> selectNameDialogFactory,
    Func<BiologicalSex?, Relative[], SelectRelativesDialog> selectRelativesDialogFactory)
    : base(
        person,
        cancellationTokenProvider,
        biologicalSexFormatter,
        nameTypeFormatter,
        nameFormatter,
        dateFormatter,
        personInfoComparer,
        alertService,
        dataConverterFactory,
        selectNameDialogFactory,
        selectRelativesDialogFactory)
  {
  }

  public Task InvokeAddPersonNameAsync() => OnAddPersonNameAsync();

  public Task InvokeEditPersonNameAsync(NameInfoItem name) => OnEditPersonNameAsync(name);
}
