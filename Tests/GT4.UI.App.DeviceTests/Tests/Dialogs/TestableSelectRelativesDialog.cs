using GT4.Core.Project.Dto;
using GT4.UI.Dialogs;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes SelectRelativesDialog's OnDialogCommand seam: EditRelationshipDateCommand pushes a modal
/// SelectDateDialog, so the test needs to await its own continuation rather than go through the
/// public DialogCommand, whose Execute is fire-and-forget.
/// </summary>
internal sealed class TestableSelectRelativesDialog : SelectRelativesDialog
{
  public TestableSelectRelativesDialog(BiologicalSex? biologicalSex, Relative[] existingRelatives, IServiceProvider serviceProvider)
    : base(biologicalSex, existingRelatives, serviceProvider)
  {
  }

  public Task InvokeDialogCommandAsync(object parameter) => OnDialogCommand(parameter);
}
