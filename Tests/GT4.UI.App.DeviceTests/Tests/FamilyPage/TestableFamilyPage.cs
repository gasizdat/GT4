using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Pages;
using GT4.UI.Utils.Formatters;
using System.Collections.Specialized;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Exposes FamilyPage's protected seams for testing. Both OnPageCommand and OnOpenPerson are
/// normally reached only through public ICommand properties, whose Execute is fire-and-forget
/// (void) and can't be awaited by a test.
/// </summary>
internal sealed class TestableFamilyPage : FamilyPage
{
  private int _CompletedLoads;

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
    // Persons returns the same ObservableCollection instance for the page's whole lifetime (even
    // across a FamilyName change), so a single subscription made up front -- before FamilyName is
    // ever set, when the getter is a side-effect-free early return -- observes every future
    // Clear()+Add() batch a background load performs.
    ((INotifyCollectionChanged)Persons).CollectionChanged += (_, _) => _CompletedLoads++;
  }

  public Task InvokePageCommandAsync(object parameter) => OnPageCommand(parameter);

  public Task InvokeOpenPersonAsync(PersonInfo familyMember) => OnOpenPerson(familyMember);

  /// <summary>
  /// How many Clear()/Add() batches a background Persons load has performed on the underlying
  /// collection. A level-triggered counter, unlike a one-shot event subscription, can't miss a
  /// completion that lands before the caller starts waiting.
  /// </summary>
  public int CompletedLoads => _CompletedLoads;

  /// <summary>
  /// Runs <paramref name="interact"/>, waits for a Persons load completion after that point
  /// (CompletedLoads is snapshotted right before <paramref name="interact"/>, so an earlier
  /// completion can't satisfy the wait), then returns the reloaded persons.
  /// </summary>
  public async Task<PersonInfo[]> ReloadPersonsAsync(Action interact, TimeSpan? timeout = null)
  {
    var loadsBefore = 0;

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      loadsBefore = _CompletedLoads;
      interact();
      _ = Persons;
    });

    await Poll.UntilAsync(
      () => Task.FromResult(_CompletedLoads),
      loads => loads > loadsBefore,
      timeout ?? TimeSpan.FromSeconds(10),
      "Persons reload did not complete; check the TestServices mock setup.");

    return await MainThread.InvokeOnMainThreadAsync(() => Persons.ToArray());
  }
}
