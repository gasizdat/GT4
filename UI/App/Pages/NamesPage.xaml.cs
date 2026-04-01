using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Items;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml.Linq;

namespace GT4.UI.Pages;

public partial class NamesPage : ContentPage
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IComparer<Name> _NameComparer;
  private readonly ObservableCollection<NameTypeInfoItem> _NameTypes;
  private readonly ObservableCollection<BiologicalSexItem> _BiologicalSexes = new();
  private readonly ICommand _PageCommand;
  private ICollection<NameInfoItem>? _Names;
  private NameTypeInfoItem _CurrentNameType;
  private NameInfoItem? _CurrentName;
  private BiologicalSexItem _CurrentBiologicalSex;

  protected NamesPage(IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _NameTypeFormatter = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _NameComparer = _ServiceProvider.GetRequiredService<IComparer<Name>>();
    _NameTypes = new((new[] { NameType.FirstName, NameType.MiddleName, NameType.LastName, NameType.AdditionalName })
      .Select(type => new NameTypeInfoItem(_NameTypeFormatter.ToString(type), type)));
    _PageCommand = new Command<object>(OnPageCommandAsync);
    _CurrentNameType = _NameTypes.First();

    var biologicalSexFormatter = _ServiceProvider.GetRequiredService<IBiologicalSexFormatter>();
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Male, biologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Female, biologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Unknown, biologicalSexFormatter));
    _CurrentBiologicalSex = _BiologicalSexes.First();

    InitializeComponent();
  }

  public NamesPage()
    : this(ServiceBuilder.DefaultServices)
  {
  }

  private async void OnPageCommandAsync(object obj)
  {

  }
  public ICollection<BiologicalSexItem> BiologicalSexes => _BiologicalSexes;

  public BiologicalSexItem CurrentBiologicalSex
  {
    get => _CurrentBiologicalSex;
    set
    {
      _CurrentBiologicalSex = value;
      OnPropertyChanged(nameof(CurrentBiologicalSex));
      Names = null;
      CurrentName = null;
    }
  }
  public ICollection<NameTypeInfoItem> NameTypes => _NameTypes;

  public ICommand PageCommand => _PageCommand;

  public ICollection<NameInfoItem>? Names
  {
    get
    {
      if (_Names != null)
      {
        return _Names;
      }
      var nameDeclension = _CurrentBiologicalSex.Info switch
      {
        BiologicalSex.Male => NameType.MaleDeclension,
        BiologicalSex.Female => NameType.FemaleDeclension,
        _ => NameType.AllNames
      };

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      _Names = _CurrentProjectProvider
        .Project
        .Names
        .GetNamesByTypeAsync(CurrentNameType.Type | nameDeclension, token)
        .Result
        .Select(name => new NameInfoItem(name, _NameTypeFormatter))
        .OrderBy(name => name.Info, _NameComparer)
        .ToArray();

      return _Names;
    }
    set
    {
      _Names = value;
      OnPropertyChanged(nameof(Names));
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
      Names = null;
      CurrentName = null;
    }
  }

  public NameInfoItem? CurrentName
  {
    get => _CurrentName;
    set
    {
      if (value?.Info.Id != _CurrentName?.Info.Id)
      {
        _CurrentName = value;
        OnPropertyChanged(nameof(CurrentName));
      }
    }
  }
}