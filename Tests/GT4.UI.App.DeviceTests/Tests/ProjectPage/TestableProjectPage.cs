using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Items;
using GT4.UI.Pages;
using GT4.UI.Utils.Formatters;
using System.Collections.Specialized;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes ProjectPage's OnPageCommand as a protected seam: it's normally reached only through the
/// public PageCommand ICommand, whose fire-and-forget Execute can't be awaited by a test.
/// </summary>
internal sealed class TestableProjectPage : ProjectPage
{
  private int _CompletedLoads;

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
    INavigationService navigationService,
    IBiologicalSexFormatter biologicalSexFormatter)
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
      navigationService,
      biologicalSexFormatter)
  {
    // Families is bound to CollectionChanged, not PropertyChanged: RefreshView() (used by the
    // "Refresh" command and OnNavigatedTo) reflectively raises OnPropertyChanged for every one of
    // ProjectPage's own public properties regardless of whether a load actually ran, so any
    // PropertyChanged-based signal fires the instant RefreshView() is called -- well before the
    // background fetch it kicks off actually finishes. EnsureFamiliesLoaded's eager _Families.Clear()
    // (synchronous, before the fetch starts) fires a Reset action; only the later _Families.AddRange
    // on fetch completion fires Add actions, so filtering to Add isolates genuine data arrival.
    ((INotifyCollectionChanged)Families).CollectionChanged += (_, e) =>
    {
      if (e.Action == NotifyCollectionChangedAction.Add)
      {
        _CompletedLoads++;
      }
    };
  }

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  /// <summary>
  /// How many background families loads have added items to the underlying collection. A
  /// level-triggered counter, unlike a one-shot event subscription, can't miss a completion that
  /// lands before the caller starts waiting. Note: a reload that legitimately resolves to zero
  /// families never fires an Add and so won't satisfy a wait -- not a concern for today's callers,
  /// which all mock a non-empty family list.
  /// </summary>
  public int CompletedLoads => _CompletedLoads;

  /// <summary>
  /// Waits for the page's own automatic first families load to finish, then returns the loaded
  /// families. CompletedLoads is level-triggered, so this is safe even if that load already
  /// finished before the caller started waiting. Deliberately doesn't offer a way to force a
  /// second reload (e.g. via the "Refresh" command): EnsureFamiliesLoaded has no guard against two
  /// overlapping background loads, so forcing one while this first one might still be in flight
  /// would race -- harmless in the real app (a user is never that fast), but exactly the kind of
  /// thing a test can hit by accident.
  /// </summary>
  public async Task<FamilyInfoItem[]> WaitForFamiliesAsync(TimeSpan? timeout = null)
  {
    await Poll.UntilAsync(
      () => Task.FromResult(_CompletedLoads),
      loads => loads >= 1,
      timeout ?? TimeSpan.FromSeconds(10),
      "Families did not finish loading; check the TestServices mock setup.");

    return await MainThread.InvokeOnMainThreadAsync(() => Families.ToArray());
  }
}
