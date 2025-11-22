using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class SelectRelativesDialog : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly IComparer<PersonInfoItem> _PersonComparer;
  private readonly BiologicalSexItem[] _BiologicalSexes;
  private readonly RelationshipTypeItem[] _RelationshipTypes;
  private readonly FilteredObservableCollection<PersonInfoItem> _Persons = new();
  private readonly ObservableCollection<object> _SelectedItems = [];
  private readonly TaskCompletionSource<PersonInfoItem[]?> _Info = new(null);
  private readonly ICommand _DialogCommand;
  private readonly HashSet<int> _ExistingRelativeIds;

  private BiologicalSexItem _BiologicalSex;
  private RelationshipTypeItem _RelationshipType;
  private long _ProjectRevision;
  private Date? _RelationshipDate;
  private string _NameFilter = string.Empty;

  private bool PersonFilter(FilteredObservableCollection<PersonInfoItem> collection, PersonInfoItem personItem)
  {
    if (!string.IsNullOrEmpty(_NameFilter))
    {
      var isMatched = personItem
        .Info
        .Names
        .Any(name => name.Value.Contains(_NameFilter, StringComparison.InvariantCultureIgnoreCase));

      if (!isMatched)
      {
        return false;
      }
    }

    if (_BiologicalSex.Info != BiologicalSex.Unknown && personItem.Info.BiologicalSex != _BiologicalSex.Info)
    {
      return false;
    }

    return true;
  }

  private async void OnDialogCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "EditRelationshipDateCommand":
        await OnRelationshipDateSetupAsync();
        break;
      case string commandName when commandName == "RemoveRelationshipDateCommand":
        RelationshipDate = null;
        break;
      case string commandName when commandName == "SelectPersonCommand":
        _Info.SetResult(_SelectedItems.Select(i => (PersonInfoItem)i).ToArray());
        break;
    }
  }

  private async Task OnRelationshipDateSetupAsync()
  {
    var dialog = new SelectDateDialog(date: _RelationshipDate, dateFormatter: _DateFormatter);

    await Navigation.PushModalAsync(dialog);
    var date = await dialog.Info;
    await Navigation.PopModalAsync();

    if (date is not null)
    {
      RelationshipDate = date;
    }
  }

  public SelectRelativesDialog(BiologicalSex? biologicalSex, Relative[] existingRelatives, IServiceProvider serviceProvider)
  {
    var biologicalSexFormatter = serviceProvider.GetRequiredService<IBiologicalSexFormatter>();
    var relationshipTypeFormatter = serviceProvider.GetRequiredService<IRelationshipTypeFormatter>();

    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    _PersonComparer = serviceProvider.GetRequiredService<IComparer<PersonInfoItem>>();
    _DialogCommand = new Command<object>(OnDialogCommand);
    _ProjectRevision = _CurrentProjectProvider.Project.ProjectRevision;
    _BiologicalSexes = new[] { BiologicalSex.Male, BiologicalSex.Female, BiologicalSex.Unknown }
      .Select(sex => new BiologicalSexItem(sex, biologicalSexFormatter))
      .ToArray();
    _BiologicalSex = _BiologicalSexes.SingleOrDefault(i => i.Info == biologicalSex, _BiologicalSexes[2]);
    _ExistingRelativeIds = [.. existingRelatives.Select(r => r.Id)];
    _RelationshipTypes = new[] {
        RelationshipType.Parent,
        RelationshipType.Child,
        RelationshipType.Spose,
        RelationshipType.AdoptiveParent,
        RelationshipType.AdoptiveChild }
      .Select(type => new RelationshipTypeItem(type, relationshipTypeFormatter))
      .ToArray();
    _RelationshipType = _RelationshipTypes.First();
    _SelectedItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(DialogButtonName));
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

  public BiologicalSexItem BioSex
  {
    get => _BiologicalSex;
    set
    {
      _BiologicalSex = value;
      _Persons.Update();
    }
  }

  public BiologicalSexItem[] BiologicalSexes => _BiologicalSexes;

  public RelationshipTypeItem RelType { get => _RelationshipType; set => _RelationshipType = value; }

  public RelationshipTypeItem[] RelationshipTypes => _RelationshipTypes;

  public IEnumerable<PersonInfoItem> Persons
  {
    get
    {
      if (_Persons.Count == 0 || _ProjectRevision != _CurrentProjectProvider.Project.ProjectRevision)
      {
        var worker = new BackgroundWorker();
        worker.DoWork += async (object? _, DoWorkEventArgs args) =>
        {
          var token = _CancellationTokenProvider.CreateDbCancellationToken();
          var persons = await _CurrentProjectProvider
            .Project
            .PersonManager
            .GetPersonInfosAsync(selectMainPhoto: false, token);
          args.Result = persons
            .Where(person => !_ExistingRelativeIds.Contains(person.Id))
            .ToArray();
        };
        worker.RunWorkerCompleted += (object? _, RunWorkerCompletedEventArgs args) =>
        {
          if (args.Error is not null)
          {
            PageAlert.ShowError(args.Error);
            return;
          }

          var persons = args.Result as PersonInfo[];
          var items = persons?
            .Select(personInfo => new PersonInfoItem(personInfo, _NameFormatter))
            .OrderBy(item => item, _PersonComparer);
          _Persons.AddRange(items ?? Enumerable.Empty<PersonInfoItem>());
          OnPropertyChanged(nameof(Persons));
        };
        worker.RunWorkerAsync();
      }

      return _Persons.Items;
    }
  }

  public IList<object> SelectedItems => _SelectedItems;

  public string DialogButtonName =>
    (_SelectedItems?.Count ?? 0) > 0 ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  public Task<PersonInfoItem[]?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  public Date? RelationshipDate
  {
    get => _RelationshipDate;
    set
    {
      _RelationshipDate = value;
      OnPropertyChanged(nameof(RelationshipDate));
    }
  }
}