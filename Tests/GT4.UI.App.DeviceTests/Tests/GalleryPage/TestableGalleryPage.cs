using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Pages;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes GalleryPage's protected command handlers for testing. Awaiting them directly bypasses
/// SafeCommand's error routing, which needs Shell.Current -- absent in this host.
/// </summary>
internal sealed class TestableGalleryPage : GalleryPage
{
  public TestableGalleryPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    INameFormatter nameFormatter,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver)
    : base(
      currentProjectProvider,
      cancellationTokenProvider,
      nameFormatter,
      alertService,
      dataConverterResolver)
  {
  }

  public Task InvokeDeleteAsync(object parameter) => OnDeleteCommandAsync(parameter);

  public Task InvokeOpenAsync(object parameter) => OnOpenCommandAsync(parameter);
}
