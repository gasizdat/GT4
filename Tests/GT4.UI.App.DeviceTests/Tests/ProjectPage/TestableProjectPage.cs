using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Pages;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes ProjectPage's OnPageCommand as a protected seam: it's normally reached only through the
/// public PageCommand ICommand, whose fire-and-forget Execute can't be awaited by a test.
/// </summary>
internal sealed class TestableProjectPage : ProjectPage
{
  public TestableProjectPage(
    INameTypeFormatter nameTypeFormatter,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    [FromKeyedServices(NameFormat.ShortPersonName)]
    IComparer<PersonInfo>? personInfoComparerByShortNames,
    IComparer<PersonInfo> personInfoComparer,
    IComparer<Name> nameComparer,
    IProjectList projectList,
    IGedcomExporter exporter,
    IGedcomImporter importer,
    IAlertService alertService,
    INavigationService navigationService)
    : base(
      nameTypeFormatter,
      cancellationTokenProvider,
      currentProjectProvider,
      personInfoComparerByShortNames,
      personInfoComparer,
      nameComparer,
      projectList,
      exporter,
      importer,
      alertService,
      navigationService)
  {
  }

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);
}
