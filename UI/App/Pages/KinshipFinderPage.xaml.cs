using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class KinshipFinderPage : ContentPage
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly INameFormatter _NameFormatter;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly INavigationService _NavigationService;
  private readonly ICommand _PageCommand;

  private PersonInfo? _PersonFrom;
  private PersonInfo? _PersonTo;
  private RelativeInfo[]? _Chain;
  private bool _Searched;
  private ProjectInfo? _LastProjectInfo;

  private async Task PickPersonAsync(Action<PersonInfo> assign)
  {
    var dialog = new SelectPersonDialog(_CancellationTokenProvider, _CurrentProjectProvider, _PersonInfoComparer, _AlertService);
    await Navigation.PushModalAsync(dialog);
    var person = await dialog.Info;
    await Navigation.PopModalAsync();

    if (person is not null)
    {
      assign(person);
      _Chain = null;
      _Searched = false;

      if (_PersonFrom is not null && _PersonTo is not null)
        await FindAsync();
      else
        this.RefreshView();
    }
  }

  private async Task FindAsync()
  {
    if (_PersonFrom is null || _PersonTo is null)
    {
      return;
    }

    // Not Loading.Run: the search awaits on the UI thread, and RefreshView must stay there.
    Loading.IsLoading = _Chain is null;
    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var chain = await _CurrentProjectProvider.Project.KinshipFinder.FindPathAsync(_PersonFrom, _PersonTo, token);

      _Chain = chain;
      _Searched = true;
      this.RefreshView();
    }
    finally
    {
      Loading.IsLoading = false;
    }
  }

  protected async Task OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "PickPersonFrom":
        await PickPersonAsync(person => _PersonFrom = person);
        break;

      case string commandName when commandName == "PickPersonTo":
        await PickPersonAsync(person => _PersonTo = person);
        break;

      case RelativeInfo relativeInfo:
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = (PersonInfo)relativeInfo });
        break;
    }
  }

  public KinshipFinderPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter,
    IComparer<PersonInfo> personInfoComparer,
    INavigationService navigationService)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _NameFormatter = nameFormatter;
    _PersonInfoComparer = personInfoComparer;
    _NavigationService = navigationService;
    _PageCommand = new SafeCommand(OnPageCommand, _AlertService);

    InitializeComponent();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  // See ProjectPage.OnNavigatedTo: the found chain shows person data that a visited-and-edited subpage
  // can change, so recompute it on return. FindAsync no-ops until both endpoints are picked.
  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    if (_LastProjectInfo != _CurrentProjectProvider.Info)
    {
      Refresh();
    }
  }

  private void Refresh()
  {
    _LastProjectInfo = _CurrentProjectProvider.Info;
    _ = SafeTask.GuardAsync(FindAsync, _AlertService);
  }

  public PageLoading Loading { get; } = new();

  public ICommand PageCommand => _PageCommand;

  public string PersonFromName => _PersonFrom is not null
    ? _NameFormatter.ToString(_PersonFrom, NameFormat.CommonPersonName)
    : UIStrings.FieldNotSelected;

  public string PersonToName => _PersonTo is not null
    ? _NameFormatter.ToString(_PersonTo, NameFormat.CommonPersonName)
    : UIStrings.FieldNotSelected;

  public RelativeInfo[] Chain => _Chain ?? [];

  public bool HasChain => Chain.Length > 0;

  // The chain's last node already carries the cumulative relationship of PersonTo to PersonFrom
  // (RelativesProvider accumulates Generation/Consanguinity across the whole walk), so it doubles as
  // the endpoint-to-endpoint summary the issue asks for -- no separate computation needed.
  public RelativeInfo? Summary => HasChain ? Chain[^1] : null;

  public bool ShowNotFound => _Searched && !HasChain;
}
