using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class ProjectRevisionsPage : ContentPage
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  private ProjectRevisionItem? _SelectedRevision;

  public ProjectRevisionsPage(ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INavigationService navigationService)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _NavigationService = navigationService;

    InitializeComponent();
  }

  public ProjectRevisionItem? SelectedRevision
  {
    get => _SelectedRevision;
    set
    {
      _SelectedRevision = value;
      OnPropertyChanged(nameof(RestoreBtnName));
    }
  }

  public string RestoreBtnName => SelectedRevision is null ?
    UIStrings.BtnNameCancel :
    string.Format(UIStrings.BtnNameRestore_1, SelectedRevision?.DateTimeText);

  public ICommand PageCommand => new SafeCommand(async (object arg) =>
  {
    switch (arg)
    {
      case string commandName when commandName == "Restore":
        if (SelectedRevision is not null)
        {
          using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
          await _CurrentProjectProvider.RestoreRevisionAsync(SelectedRevision.Info, token);
        }

        await _NavigationService.GoToAsync(UIRoutes.GetRoute<ProjectPage>());
        break;

      case string commandName when commandName == "Refresh":
        this.RefreshView();
        break;
    }
  }, _AlertService);

  public ICommand DeleteRevisionCommand => new SafeCommand(async (object item) =>
  {
    if (item is ProjectRevisionItem projectRevisionItem)
    {
      using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
      await _CurrentProjectProvider.RemoveRevisionAsync(projectRevisionItem.Info, token);
      OnPropertyChanged(nameof(Revisions));
    }
  }, _AlertService);

  public IEnumerable<ProjectRevisionItem> Revisions => _CurrentProjectProvider
    .Revisions
    .Select(r => new ProjectRevisionItem(r));

  private void OnNavigatedTo(object sender, NavigatedToEventArgs e)
  {
    OnPropertyChanged(nameof(Revisions));
  }
}