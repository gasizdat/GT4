using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class PersonPage : ContentPage
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly ICommand _PageCommand;
  private readonly ObservableCollection<RelativeInfoItem> _Relatives = new();
  private PersonFullInfo _PersonFullInfo = PersonFullInfo.Empty;

  public PersonPage(IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _RelationshipTypeFormatter = _ServiceProvider.GetRequiredService<IRelationshipTypeFormatter>();
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _DateSpanFormatter = _ServiceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormatter = _ServiceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = _ServiceProvider.GetRequiredService<INameFormatter>();
    _PageCommand = new Command(OnPageCommand);

    InitializeComponent();
  }

  public PersonPage()
    : this(ServiceBuilder.DefaultServices)
  {
  }

  public ICommand PageCommand => _PageCommand;

  public string EditPersonToolbarItemName => string.Format(UIStrings.MenuItemNameEdit_1, ShortName);

  public string RemovePersonToolbarItemName => string.Format(UIStrings.MenuItemNameRemove_1, ShortName);

  public string ShortName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.ShortPersonName);

  public string CommonName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.CommonPersonName);

  public string FullName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.FullPersonName);

  public bool ShowFullName => CommonName != FullName;

  public string BirthDate => _DateFormatter.ToString(_PersonFullInfo.BirthDate);

  public string DeathDate => _DateFormatter.ToString(_PersonFullInfo.DeathDate);

  public bool ShowDeathDate => _PersonFullInfo.DeathDate.HasValue;

  public string Age
  {
    get
    {
      var age = _PersonFullInfo.DeathDate.GetValueOrDefault(Date.Now) - _PersonFullInfo.BirthDate;
      return _DateSpanFormatter.ToString(age);
    }
  }

  public ICollection Relatives => _Relatives;

  public ImageSource Photo => _PersonFullInfo?.MainPhoto is null
    ? GetDefaultImage()
    : ImageUtils.ImageFromBytes(_PersonFullInfo.MainPhoto.Content);

  public PersonInfo PersonInfo
  {
    set
    {
      using var backgroundWorker = new BackgroundWorker();
      backgroundWorker.DoWork += async (object? _, DoWorkEventArgs args) =>
      {
        var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var project = _CurrentProjectProvider.Project;
        var person = await project.PersonManager.GetPersonFullInfoAsync(value, token);
        var siblings = await _CurrentProjectProvider
          .Project
          .PersonManager
          .GetSiblings(person, token);

        args.Result = (person, siblings);
      };
      backgroundWorker.RunWorkerCompleted += async (object? _, RunWorkerCompletedEventArgs args) =>
      {
        if (args.Error is not null)
        {
          await PageAlert.ShowError(args.Error);
          await Shell.Current.GoToAsync("..", true);
          return;
        }
        if (args.Cancelled || args.Result is null)
        {
          await PageAlert.ShowConfirmation("Operation cancelled");
          await Shell.Current.GoToAsync("..", true);
          return;
        }

        var (person, siblings) = ((PersonFullInfo, Siblings))args.Result;
        _PersonFullInfo = person;
        _Relatives.Clear();

        void Add(IEnumerable<RelativeInfo> relatives)
        {
          foreach (var relative in relatives.OrderBy(r => r.BiologicalSex))
          {
            var relativeInfoItem = new RelativeInfoItem(
              _PersonFullInfo.BirthDate, relative, _DateFormatter, _RelationshipTypeFormatter, _NameFormatter);
            _Relatives.Add(relativeInfoItem);
          }
        }

        Add(_PersonFullInfo.RelativeInfos.Where(r => r.Type == RelationshipType.Spose));
        Add(PersonManager.Parent(_PersonFullInfo));
        Add(PersonManager.AdoptiveParent(person));
        Add(siblings.Native);
        Add(siblings.ByFather);
        Add(siblings.ByMother);
        Add(siblings.Step);
        Add(siblings.Adoptive);
        Add(PersonManager.Children(person));
        Add(PersonManager.AdoptiveChildren(person));

        Utils.RefreshView(this);
      };

      backgroundWorker.RunWorkerAsync();
    }
  }

  private async void OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string name when name == "RemovePerson":
        break;
      case string name when name == "EditPerson":
        await OnPersonEdit();
        break;
      case string name when name == "Refresh":
        PersonInfo = _PersonFullInfo;
        break;
      case RelativeInfoItem relativeInfoItem:
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { { "PersonInfo", relativeInfoItem.Info } });
        break;
    }
  }

  private async Task OnPersonEdit()
  {
    var dialog = new CreateOrUpdatePersonDialog(_PersonFullInfo, _ServiceProvider);
    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
      if (info is null)
      {
        return;
      }

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _CurrentProjectProvider
        .Project
        .PersonManager
        .UpdatePersonAsync(info, token);

      PersonInfo = info;
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
  }

  private ImageSource GetDefaultImage()
  {
    return _PersonFullInfo.BiologicalSex switch
    {
      BiologicalSex.Male => ImageUtils.ImageFromRawResource("male_stub.png"),
      BiologicalSex.Female => ImageUtils.ImageFromRawResource("female_stub.png"),
      _ => ImageUtils.ImageFromRawResource("project_icon.png")
    };
  }
}