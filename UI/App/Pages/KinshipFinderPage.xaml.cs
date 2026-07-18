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
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICommand _PageCommand;

  private PersonInfo? _PersonFrom;
  private PersonInfo? _PersonTo;
  private RelativeInfo[]? _Chain;
  private bool _Searched;

  private async Task PickPersonAsync(Action<PersonInfo> assign)
  {
    var dialog = new SelectPersonDialog(_ServiceProvider);
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

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var chain = await _CurrentProjectProvider.Project.KinshipFinder.FindPathAsync(_PersonFrom, _PersonTo, token);

    _Chain = chain;
    _Searched = true;
    this.RefreshView();
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
    }
  }

  public KinshipFinderPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    INameFormatter nameFormatter,
    IServiceProvider serviceProvider)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _NameFormatter = nameFormatter;
    _ServiceProvider = serviceProvider;
    _PageCommand = new SafeCommand(OnPageCommand, _AlertService);

    InitializeComponent();
  }

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
