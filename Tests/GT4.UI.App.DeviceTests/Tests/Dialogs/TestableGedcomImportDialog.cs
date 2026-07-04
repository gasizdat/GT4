using GT4.UI.Dialogs;

namespace GT4.UI.DeviceTests;

/// <summary>Exposes GedcomImportDialog's protected OnBackButtonPressed override for testing.</summary>
internal sealed class TestableGedcomImportDialog : GedcomImportDialog
{
  public TestableGedcomImportDialog(string importingProjectName) : base(importingProjectName)
  {
  }

  public bool InvokeOnBackButtonPressed() => OnBackButtonPressed();
}
