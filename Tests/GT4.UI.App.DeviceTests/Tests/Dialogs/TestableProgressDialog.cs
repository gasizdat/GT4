using GT4.UI.Abstraction;
using GT4.UI.Dialogs;

namespace GT4.UI.DeviceTests;

internal sealed class TestableProgressDialog : ProgressDialog
{
  public TestableProgressDialog(string heading, string subject, string inProgressText, IAlertService alertService)
    : base(heading, subject, inProgressText, alertService)
  {
  }

  public bool InvokeOnBackButtonPressed() => OnBackButtonPressed();
}
