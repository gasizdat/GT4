using GT4.Core.Project.Dto;
using GT4.UI.Dialogs;
using GT4.UI.Items;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes CreateOrUpdatePersonDialog's OnAddPersonNameAsync/OnEditPersonNameAsync seams: both push a
/// modal SelectNameDialog, so the test needs to await its own continuation rather than go through the
/// public DialogCommand, whose Execute is fire-and-forget.
/// </summary>
internal sealed class TestableCreateOrUpdatePersonDialog : CreateOrUpdatePersonDialog
{
  public TestableCreateOrUpdatePersonDialog(PersonFullInfo? person, IServiceProvider serviceProvider)
    : base(person, serviceProvider)
  {
  }

  public Task InvokeAddPersonNameAsync() => OnAddPersonNameAsync();

  public Task InvokeEditPersonNameAsync(NameInfoItem name) => OnEditPersonNameAsync(name);
}
