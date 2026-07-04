using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Pages;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes FamilyPage's protected seams for testing. Both OnPageCommand and OnOpenPerson are
/// normally reached only through public ICommand properties, whose Execute is fire-and-forget
/// (void) and can't be awaited by a test.
/// </summary>
internal sealed class TestableFamilyPage : FamilyPage
{
  public TestableFamilyPage(
    IServiceProvider serviceProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    [FromKeyedServices(NameFormat.ShortPersonName)]
    IComparer<PersonInfo>? personInfoComparerByShortNames,
    IComparer<PersonInfo> personInfoComparer,
    IAlertService alertService,
    INavigationService navigationService)
    : base(
      serviceProvider,
      cancellationTokenProvider,
      currentProjectProvider,
      personInfoComparerByShortNames,
      personInfoComparer,
      alertService,
      navigationService)
  {
  }

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  public Task InvokeOpenPersonAsync(PersonInfo familyMember) => OnOpenPerson(familyMember);
}
