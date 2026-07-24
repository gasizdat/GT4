using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class SelectPersonDialog : ContentPage
{
  public record class Factory(
    ICancellationTokenProvider CancellationTokenProvider,
    ICurrentProjectProvider CurrentProjectProvider,
    IComparer<PersonInfo> PersonInfoComparer,
    IAlertService AlertService)
  {
    public SelectPersonDialog Create() =>
      new SelectPersonDialog(CancellationTokenProvider, CurrentProjectProvider, PersonInfoComparer, AlertService);
  }

  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IAlertService _AlertService;
  private readonly FilteredObservableCollection<PersonInfo> _Persons = new();
  private readonly TaskCompletionSource<PersonInfo?> _Info = new(null);
  private readonly ICommand _DialogCommand;

  private string? _ProjectRevision;
  private string _NameFilter = string.Empty;
  private PersonInfo? _SelectedPerson;

  private bool PersonFilter(FilteredObservableCollection<PersonInfo> collection, PersonInfo personItem) =>
    string.IsNullOrEmpty(_NameFilter) ||
    personItem.Names.Any(name => name.Value.Contains(_NameFilter, StringComparison.InvariantCultureIgnoreCase));

  protected Task OnDialogCommand(object obj)
  {
    if (obj is string commandName && commandName == "SelectPersonCommand")
    {
      _Info.SetResult(_SelectedPerson);
    }

    return Task.CompletedTask;
  }

  public SelectPersonDialog(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IComparer<PersonInfo> personInfoComparer,
    IAlertService alertService)
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _PersonInfoComparer = personInfoComparer;
    _AlertService = alertService;
    _DialogCommand = new SafeCommand(OnDialogCommand, _AlertService);
    _ProjectRevision = _CurrentProjectProvider.Project.ProjectRevision;
    _Persons.Filter = PersonFilter;

    InitializeComponent();
  }

  public string NameFilter
  {
    get => _NameFilter;
    set
    {
      _NameFilter = value;
      _Persons.Update();
    }
  }

  public IEnumerable<PersonInfo> Persons
  {
    get
    {
      async Task AddPersonInfoItemsAsync()
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var persons = await _CurrentProjectProvider
          .Project
          .PersonManager
          .GetPersonInfosAsync(selectMainPhoto: false, token);

        var items = persons.OrderBy(item => item, _PersonInfoComparer);

        MainThread.BeginInvokeOnMainThread(() => _Persons.AddRange(items));
      }

      if (_Persons.Count == 0 || _ProjectRevision != _CurrentProjectProvider.Project.ProjectRevision)
      {
        SafeTask.Run(AddPersonInfoItemsAsync, _AlertService);
      }

      return _Persons.Items;
    }
  }

  public PersonInfo? SelectedPerson
  {
    get => _SelectedPerson;
    set
    {
      _SelectedPerson = value;
      OnPropertyChanged(nameof(SelectedPerson));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public string DialogButtonName => _SelectedPerson is not null ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  public Task<PersonInfo?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;
}
