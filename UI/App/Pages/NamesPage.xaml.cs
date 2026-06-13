using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

public partial class NamesPage : ContentPage
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IComparer<Name> _NameComparer;
  private readonly ObservableCollection<NameTypeInfoItem> _NameTypes;
  private readonly ObservableCollection<BiologicalSexItem> _BiologicalSexes = new();
  private readonly FilteredObservableCollection<Name> _Names = new();
  private readonly ICommand _EditNameCommand;
  private readonly ICommand _DeleteNameCommand;
  private readonly ICommand _PageCommand;
  private NameTypeInfoItem _CurrentNameType;
  private Name? _CurrentName;
  private int? _CurrentNameId;
  private BiologicalSexItem _CurrentBiologicalSex;
  private bool _UpdateNames = true;
  private string _NameFilter = string.Empty;

  public NamesPage(IServiceProvider serviceProvider)
  {
    var nameTypes = new[]
    {
      NameType.FamilyName,
      NameType.FirstName,
      NameType.Patronymic,
      NameType.LastName,
    };
    var nameTypeFormatter = serviceProvider.GetRequiredService<INameTypeFormatter>();
    _ServiceProvider = serviceProvider;
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _NameComparer = _ServiceProvider.GetRequiredService<IComparer<Name>>();
    _NameTypes = new(nameTypes.Select(type => new NameTypeInfoItem(nameTypeFormatter.ToString(type), type)));
    _EditNameCommand = new SafeCommand(OnEditCommandAsync);
    _DeleteNameCommand = new SafeCommand(OnDeleteCommandAsync);
    _PageCommand = new SafeCommand(OnPageCommandAsync);
    _CurrentNameType = _NameTypes.First();
    _Names.Filter = NamesFilter;

    var biologicalSexFormatter = _ServiceProvider.GetRequiredService<IBiologicalSexFormatter>();
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Male, biologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Female, biologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Unknown, biologicalSexFormatter));
    _CurrentBiologicalSex = _BiologicalSexes.First();

    InitializeComponent();
  }

  protected async Task OnEditCommandAsync(object obj)
  {
    if (obj is Name name)
    {
      await CreateOrUpdateNameDialog.UpdateNameAsync(name, _ServiceProvider, Navigation);
      RequestUpdateNames(name);
    }
  }

  protected async Task OnDeleteCommandAsync(object obj)
  {
    if (obj is Name name)
    {
      var project = _CurrentProjectProvider.Project;
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();

      try
      {
        await project
          .Names
          .RemoveNameWithSubnamesAsync(name, token);
        RequestUpdateNames(name);
      }
      catch
      {
        async Task CheckIfNameIsUsed(Name name)
        {
          var persons = await project
            .PersonManager
            .GetPersonInfosByNameAsync(name, false, token);
          if (persons.Any())
          {
            var nameFormatter = _ServiceProvider.GetRequiredService<INameFormatter>();
            var personsList = persons
              .Take(3)
              .Select(p => nameFormatter.ToString(p, NameFormat.CommonPersonName));
            var errorMessage = string.Format(UIStrings.ErrorNameIsShared_2, name.Value, string.Join(", ", personsList));
            throw new ApplicationException(errorMessage);
          }
        }

        await CheckIfNameIsUsed(name);

        var names = await project
          .Names
          .TryGetNameWithSubnamesByIdAsync(name.Id, token);

        foreach (var subname in names!)
        {
          await CheckIfNameIsUsed(subname);
        }

        throw;
      }
    }
  }

  protected async Task OnAddName()
  {
    var nameType = CurrentNameType.Type switch
    {
      NameType.FamilyName or NameType.LastName => NameType.FamilyName,
      NameType.FirstName when _CurrentBiologicalSex.Info == BiologicalSex.Male => NameType.FirstName | NameType.MaleDeclension,
      NameType.FirstName when _CurrentBiologicalSex.Info == BiologicalSex.Female => NameType.FirstName | NameType.FemaleDeclension,
      NameType.Patronymic => NameType.FirstName | NameType.MaleDeclension,
      _ => throw new ApplicationException(nameof(OnPageCommandAsync))
    };

    var dialog = new CreateOrUpdateNameDialog(nameType, _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
    {
      return;
    }

    var project = _CurrentProjectProvider.Project;
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var addedName = nameType switch
    {
      NameType.FamilyName =>
        await project
          .FamilyManager
          .AddFamilyAsync(familyName: info.Name, maleLastName: info.MaleName, femaleLastName: info.FemaleName, token),

      NameType.FirstName | NameType.MaleDeclension => await project.
          Names.
          AddFirstMaleNameAsync(firstName: info.Name, malePatronymic: info.MaleName, femalePatronymic: info.FemaleName, token),

      NameType.FirstName | NameType.FemaleDeclension => await project.Names.AddFirstFemaleNameAsync(info.Name, token),

      _ => null
    };

    if (addedName is null)
    {
      return;
    }

    var names = await project.Names.TryGetNameWithSubnamesByIdAsync(addedName.Id, token);
    switch (_CurrentNameType.Type)
    {
      case NameType.FirstName:
        RequestUpdateNames(addedName);
        break;

      case NameType.FamilyName:
        RequestUpdateNames(names?.SingleOrDefault(n => n.Type == NameType.FamilyName));
        break;

      case NameType.LastName when _CurrentBiologicalSex.Info == BiologicalSex.Male:
        RequestUpdateNames(names?.SingleOrDefault(n => n.Type == (NameType.LastName | NameType.MaleDeclension)));
        break;

      case NameType.LastName when _CurrentBiologicalSex.Info == BiologicalSex.Female:
        RequestUpdateNames(names?.SingleOrDefault(n => n.Type == (NameType.LastName | NameType.FemaleDeclension)));
        break;

      case NameType.Patronymic when _CurrentBiologicalSex.Info == BiologicalSex.Male:
        RequestUpdateNames(names?.SingleOrDefault(n => n.Type == (NameType.Patronymic | NameType.MaleDeclension)));
        break;

      case NameType.Patronymic when _CurrentBiologicalSex.Info == BiologicalSex.Female:
        RequestUpdateNames(names?.SingleOrDefault(n => n.Type == (NameType.Patronymic | NameType.FemaleDeclension)));
        break;

      default:
        RequestUpdateNames();
        break;
    }
  }

  protected async Task OnPageCommandAsync(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "AddName":
        await OnAddName();
        break;
    }
  }

  private bool NamesFilter(FilteredObservableCollection<Name> _, Name name)
  {
    if (!string.IsNullOrEmpty(_NameFilter))
    {
      var isMatched = name
        .Value
        .Contains(_NameFilter, StringComparison.InvariantCultureIgnoreCase);

      if (!isMatched)
      {
        return false;
      }
    }

    return true;
  }

  protected void RequestUpdateNames(Name? selectedName = null)
  {
    _UpdateNames = true;
    _CurrentNameId = selectedName?.Id;
    OnPropertyChanged(nameof(Names));
  }

  public ICollection<BiologicalSexItem> BiologicalSexes => _BiologicalSexes;

  public BiologicalSexItem CurrentBiologicalSex
  {
    get => _CurrentBiologicalSex;
    set
    {
      _CurrentBiologicalSex = value;
      OnPropertyChanged(nameof(CurrentBiologicalSex));
      RequestUpdateNames();
    }
  }

  public ICollection<NameTypeInfoItem> NameTypes => _NameTypes;

  public ICommand EditNameCommand => _EditNameCommand;

  public ICommand DeleteNameCommand => _DeleteNameCommand;

  public ICommand PageCommand => _PageCommand;

  public IEnumerable<Name> Names
  {
    get
    {
      async Task AddNameItemsAsync()
      {
        var nameDeclension = _CurrentBiologicalSex.Info switch
        {
          _ when CurrentNameType.Type == NameType.FamilyName => NameType.FamilyName,
          BiologicalSex.Male => NameType.MaleDeclension,
          BiologicalSex.Female => NameType.FemaleDeclension,
          _ => NameType.AllNames
        };

        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var names = _CurrentProjectProvider
          .Project
          .Names
          .GetNamesByTypeAsync(CurrentNameType.Type | nameDeclension, token)
          .Result
          .OrderBy(name => name, _NameComparer)
          .ToArray();

        MainThread.BeginInvokeOnMainThread(() =>
        {
          _Names.Clear();
          _Names.AddRange(names);
          OnPropertyChanged(nameof(CurrentName));
        });
      }

      if (_UpdateNames)
      {
        _UpdateNames = false;
        // AddNameItemsAsync reads the project document on a background thread; SafeTask swallows the
        // teardown race (project closed while backgrounding) and surfaces any real failure.
        _ = SafeTask.Run(AddNameItemsAsync);
      }

      return _Names.Items;
    }
  }

  public NameTypeInfoItem CurrentNameType
  {
    get => _CurrentNameType;
    set
    {
      if (_CurrentNameType == value)
        return;

      _CurrentNameType = value;
      RequestUpdateNames();
      OnPropertyChanged(nameof(CurrentNameType));
    }
  }

  public Name? CurrentName
  {
    get
    {
      if (_CurrentNameId != _CurrentName?.Id)
      {
        _CurrentName = _Names?.SingleOrDefault(n => n.Id == _CurrentNameId);
      }

      return _CurrentName;
    }
  }

  public string NameFilter
  {
    get => _NameFilter;
    set
    {
      _NameFilter = value;
      _Names.Update();
    }
  }
}