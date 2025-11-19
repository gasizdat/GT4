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
  private readonly BiologicalSexItem[] _BiologicalSexes;
  private readonly RelationshipTypeItem[] _RelationshipTypes;
  private readonly List<PersonInfoItem> _Persons = [];
  private readonly ObservableCollection<object> _SelectedItems = [];
  private readonly TaskCompletionSource<PersonInfoItem[]?> _Info = new(null);
  private readonly ICommand _DialogCommand;

  private BiologicalSexItem _BiologicalSex;
  private RelationshipTypeItem _RelationshipType;
  private long _ProjectRevision;
  private Date? _RelationshipDate;

  private bool PersonSelector(PersonInfoItem personItem)
  {
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

  public SelectRelativesDialog(BiologicalSex? biologicalSex, IServiceProvider serviceProvider)
  {
    var biologicalSexFormatter = serviceProvider.GetRequiredService<IBiologicalSexFormatter>();
    var relationshipTypeFormatter = serviceProvider.GetRequiredService<IRelationshipTypeFormatter>();

    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    _DialogCommand = new Command<object>(OnDialogCommand);
    _ProjectRevision = _CurrentProjectProvider.Project.ProjectRevision;
    _BiologicalSexes = new[] { BiologicalSex.Male, BiologicalSex.Female, BiologicalSex.Unknown }
      .Select(sex => new BiologicalSexItem(sex, biologicalSexFormatter))
      .ToArray();
    _BiologicalSex = _BiologicalSexes.SingleOrDefault(i => i.Info == biologicalSex, _BiologicalSexes[2]);
    _RelationshipTypes = new[] {
        RelationshipType.Parent,
        RelationshipType.Child, 
        RelationshipType.Spose,
        RelationshipType.AdoptiveParent,
        RelationshipType.AdoptiveChild }
      .Select(type => new RelationshipTypeItem(type, relationshipTypeFormatter))
      .ToArray();
    _RelationshipType = _RelationshipTypes.First();
    _SelectedItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(DialogBtnName));

    InitializeComponent();
  }

  public string NameFilter { get; set; } = string.Empty;

  public BiologicalSexItem BioSex { get => _BiologicalSex; set => _BiologicalSex = value; }

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
        PersonInfo[]? persons = null;
        worker.DoWork += async (_, _) =>
        {
          var token = _CancellationTokenProvider.CreateDbCancellationToken();
          persons = await _CurrentProjectProvider
            .Project
            .PersonManager
            .GetPersonInfosAsync(selectMainPhoto: false, token);

        };
        worker.RunWorkerCompleted += (s, e) =>
        {
          _Persons.AddRange(persons?.Select(personInfo => new PersonInfoItem(personInfo, _NameFormatter)) ?? []);
          OnPropertyChanged(nameof(Persons));
        };
        worker.RunWorkerAsync();
      }

      return _Persons.Where(PersonSelector);
    }
  }

  public IList<object> SelectedItems => _SelectedItems;

  public string DialogBtnName =>
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