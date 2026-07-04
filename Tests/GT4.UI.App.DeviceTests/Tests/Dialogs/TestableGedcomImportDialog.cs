using GT4.UI.Dialogs;

namespace GT4.UI.DeviceTests;

internal sealed class TestableGedcomImportDialog : GedcomImportDialog
{
  public TestableGedcomImportDialog(string importingProjectName) : base(importingProjectName)
  {
  }

  public bool InvokeOnBackButtonPressed() => OnBackButtonPressed();
}
