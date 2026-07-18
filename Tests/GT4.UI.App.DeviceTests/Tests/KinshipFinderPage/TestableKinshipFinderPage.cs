using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes KinshipFinderPage's OnPageCommand seam: PickPersonFrom/PickPersonTo push a modal
/// SelectPersonDialog, so a test needs to await its own continuation rather than go through the
/// public PageCommand, whose Execute is fire-and-forget.
/// </summary>
internal sealed class TestableKinshipFinderPage : KinshipFinderPage
{
  public TestableKinshipFinderPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter,
    IServiceProvider serviceProvider)
    : base(currentProjectProvider, cancellationTokenProvider, alertService, nameFormatter, serviceProvider)
  {
  }

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);
}
