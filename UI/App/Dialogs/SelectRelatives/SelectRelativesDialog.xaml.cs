using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class SelectRelativesDialog : ContentPage
{
  public record class Factory(
    ICancellationTokenProvider CancellationTokenProvider,
    ICurrentProjectProvider CurrentProjectProvider,
    IDateFormatter DateFormatter,
    IComparer<PersonInfo> PersonInfoComparer,
    IAlertService AlertService,
    IBiologicalSexFormatter BiologicalSexFormatter,
    IRelationshipTypeFormatter RelationshipTypeFormatter)
  {
    public SelectRelativesDialog Create(BiologicalSex? biologicalSex, Relative[] existingRelatives) => 
      new SelectRelativesDialog(this, biologicalSex, existingRelatives);
  }

  private readonly Factory _Factory;
  private readonly BiologicalSexItem[] _BiologicalSexes;
  private readonly RelationshipTypeItem[] _RelationshipTypes;
  private readonly FilteredObservableCollection<PersonInfo> _Persons = new();
  private readonly ObservableCollection<object> _SelectedItems = [];
  private readonly TaskCompletionSource<RelativeInfo[]?> _Info = new(null);
  private readonly ICommand _DialogCommand;
  private readonly Relative[] _ExistingRelatives;

  private BiologicalSexItem _BiologicalSex;
  private RelationshipTypeItem _RelationshipType;
  private string? _ProjectRevision;
  private Date? _RelationshipDate;
  private string _NameFilter = string.Empty;

  private bool PersonFilter(FilteredObservableCollection<PersonInfo> collection, PersonInfo personItem)
  {
    if (!string.IsNullOrEmpty(_NameFilter))
    {
      var isMatched = personItem
        .Names
        .Any(name => name.Value.Contains(_NameFilter, StringComparison.InvariantCultureIgnoreCase));

      if (!isMatched)
      {
        return false;
      }
    }

    if (_BiologicalSex.Info != BiologicalSex.Unknown && personItem.BiologicalSex != _BiologicalSex.Info)
    {
      return false;
    }

    return true;
  }

  protected async Task OnDialogCommand(object obj)
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
        var generation = new Generation(_RelationshipType.Info);
        var relatives = _SelectedItems
          .Select(i => (PersonInfo)i)
          .Select(person => new RelativeInfo(
            person: person,
            type: _RelationshipType.Info,
            date: _RelationshipDate.HasValue ? _RelationshipDate.Value : null,
            generation: generation,
            consanguinity: Consanguinity.Zero));

        _Info.SetResult([.. relatives]);
        break;
    }
  }

  private async Task OnRelationshipDateSetupAsync()
  {
    var dialog = new SelectDateDialog(
      date: _RelationshipDate,
      dateFormatter: _Factory.DateFormatter,
      alertService: _Factory.AlertService);

    await Navigation.PushModalAsync(dialog);
    var date = await dialog.Info;
    await Navigation.PopModalAsync();

    if (date is not null)
    {
      RelationshipDate = date;
    }
  }

  protected SelectRelativesDialog(
    Factory factory,
    BiologicalSex? biologicalSex,
    Relative[] existingRelatives)
  {
    _Factory = factory;
    _DialogCommand = new SafeCommand(OnDialogCommand, _Factory.AlertService);
    _ProjectRevision = _Factory.CurrentProjectProvider.Project.ProjectRevision;
    _BiologicalSexes = new[] { BiologicalSex.Male, BiologicalSex.Female, BiologicalSex.Unknown }
      .Select(sex => new BiologicalSexItem(sex, _Factory.BiologicalSexFormatter))
      .ToArray();
    _BiologicalSex = _BiologicalSexes.SingleOrDefault(i => i.Info == biologicalSex, _BiologicalSexes[2]);
    _ExistingRelatives = existingRelatives;
    _RelationshipTypes = new[] {
        RelationshipType.Parent,
        RelationshipType.Child,
        RelationshipType.Spouse,
        RelationshipType.AdoptiveParent,
        RelationshipType.AdoptiveChild }
      .Select(type => new RelationshipTypeItem(type, _Factory.RelationshipTypeFormatter))
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

  public IEnumerable<PersonInfo> Persons
  {
    get
    {
      async Task AddPersonInfoItemsAsync()
      {
        using var token = _Factory.CancellationTokenProvider.CreateDbCancellationToken();
        var persons = await _Factory.CurrentProjectProvider
          .Project
          .PersonManager
          .GetPersonInfosAsync(selectMainPhoto: false, token);

        var items = persons
          .Where(p =>
          {
            using var token = _Factory.CancellationTokenProvider.CreateDbCancellationToken();
            foreach (var relative in _ExistingRelatives)
            {
              if (relative.Type == RelationshipType.Parent)
              {
                var hasCommonAncestors = _Factory.CurrentProjectProvider
                  .Project
                  .Relatives
                  .HasCommonAncestorsAsync(p, relative, token)
                  .Result;
                if (hasCommonAncestors)
                {
                  return false;
                }
              }
              else if (relative.Id == p.Id)
              {
                return false;
              }
            }

            return true;
          })
          .OrderBy(item => item, _Factory.PersonInfoComparer);

        MainThread.BeginInvokeOnMainThread(() => _Persons.AddRange(items));
      }

      if (_Persons.Count == 0 || _ProjectRevision != _Factory.CurrentProjectProvider.Project.ProjectRevision)
      {
        SafeTask.Run(AddPersonInfoItemsAsync, _Factory.AlertService);
      }

      return _Persons.Items;
    }
  }

  public IList<object> SelectedItems => _SelectedItems;

  public string DialogButtonName =>
    (_SelectedItems?.Count ?? 0) > 0 ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  public Task<RelativeInfo[]?> Info => _Info.Task;

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