using GT4.UI.Abstraction;
using GT4.UI.Dialogs;

namespace GT4.UI.DeviceTests;

internal sealed class TestableGedcomImportDialog : GedcomImportDialog
{
  public TestableGedcomImportDialog(string importingProjectName, IAlertService alertService)
    : base(importingProjectName, alertService)
  {
  }

  public bool InvokeOnBackButtonPressed() => OnBackButtonPressed();
}
