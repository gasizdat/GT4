using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

internal delegate TestableSelectRelativesDialog TestableSelectRelativesDialogFactory(BiologicalSex? biologicalSex, Relative[] existingRelatives);

/// <summary>
/// Exposes SelectRelativesDialog's OnDialogCommand seam: EditRelationshipDateCommand pushes a modal
/// SelectDateDialog, so the test needs to await its own continuation rather than go through the
/// public DialogCommand, whose Execute is fire-and-forget.
/// </summary>
internal sealed class TestableSelectRelativesDialog : SelectRelativesDialog
{
  public TestableSelectRelativesDialog(
    BiologicalSex? biologicalSex,
    Relative[] existingRelatives,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IDateFormatter dateFormatter,
    IComparer<PersonInfo> personInfoComparer,
    IAlertService alertService,
    IBiologicalSexFormatter biologicalSexFormatter,
    IRelationshipTypeFormatter relationshipTypeFormatter)
    : base(
        biologicalSex,
        existingRelatives,
        cancellationTokenProvider,
        currentProjectProvider,
        dateFormatter,
        personInfoComparer,
        alertService,
        biologicalSexFormatter,
        relationshipTypeFormatter)
  {
  }

  public Task InvokeDialogCommandAsync(object parameter) => OnDialogCommand(parameter);
}
