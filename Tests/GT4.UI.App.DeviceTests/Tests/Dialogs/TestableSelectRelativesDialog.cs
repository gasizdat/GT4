using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes SelectRelativesDialog's OnDialogCommand seam: EditRelationshipDateCommand pushes a modal
/// SelectDateDialog, so the test needs to await its own continuation rather than go through the
/// public DialogCommand, whose Execute is fire-and-forget.
/// </summary>
internal sealed class TestableSelectRelativesDialog : SelectRelativesDialog
{
  public new record class Factory(
    ICancellationTokenProvider CancellationTokenProvider,
    ICurrentProjectProvider CurrentProjectProvider,
    IDateFormatter DateFormatter,
    IComparer<PersonInfo> PersonInfoComparer,
    IAlertService AlertService,
    IBiologicalSexFormatter BiologicalSexFormatter,
    IRelationshipTypeFormatter RelationshipTypeFormatter) 
    : SelectRelativesDialog.Factory(
      CancellationTokenProvider,
      CurrentProjectProvider,
      DateFormatter,
      PersonInfoComparer,
      AlertService,
      BiologicalSexFormatter,
      RelationshipTypeFormatter)
  {
    public new TestableSelectRelativesDialog Create(BiologicalSex? biologicalSex, Relative[] existingRelatives) =>
      new TestableSelectRelativesDialog(this, biologicalSex, existingRelatives);
  }

  public TestableSelectRelativesDialog(
    TestableSelectRelativesDialog.Factory factory,
    BiologicalSex? biologicalSex,
    Relative[] existingRelatives)
    : base(
        factory,
        biologicalSex,
        existingRelatives)
  {
  }

  public Task InvokeDialogCommandAsync(object parameter) => OnDialogCommand(parameter);
}
