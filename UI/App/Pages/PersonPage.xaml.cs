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
  private readonly IDateSpanFormatter _DateSpanFormmater;
  private readonly IDateFormatter _DateFormmater;
  private readonly INameFormatter _NameFormmater;
  private readonly ICommand _PageCommand;
  private readonly ObservableCollection<RelativeInfoItem> _Relatives = new();
  private PersonFullInfo _PersonFullInfo = PersonFullInfo.Empty;

  public PersonPage(IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _RelationshipTypeFormatter = _ServiceProvider.GetRequiredService<IRelationshipTypeFormatter>();
    _CurrentProjectProvider = _ServiceProvider.GetRequiredService<ICurrentProjectProvider>();
    _DateSpanFormmater = _ServiceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormmater = _ServiceProvider.GetRequiredService<IDateFormatter>();
    _NameFormmater = _ServiceProvider.GetRequiredService<INameFormatter>();
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

  public string ShortName => _NameFormmater.ToString(_PersonFullInfo, NameFormat.ShortPersonName);

  public string CommonName => _NameFormmater.ToString(_PersonFullInfo, NameFormat.CommonPersonName);

  public string FullName => _NameFormmater.ToString(_PersonFullInfo, NameFormat.FullPersonName);

  public bool ShowFullName => CommonName != FullName;

  public string BirthDate => _DateFormmater.ToString(_PersonFullInfo.BirthDate);

  public string DeathDate => _DateFormmater.ToString(_PersonFullInfo.DeathDate);

  public bool ShowDeathDate => _PersonFullInfo.DeathDate.HasValue;

  public string Age
  {
    get
    {
      var age = _PersonFullInfo.DeathDate.GetValueOrDefault(Date.Now) - _PersonFullInfo.BirthDate;
      return _DateSpanFormmater.ToString(age);
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
      var backgroundWorker = new BackgroundWorker();
      backgroundWorker.DoWork += async (object? _, DoWorkEventArgs args) =>
      {
        var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var project = _CurrentProjectProvider.Project;
        var person = await project.PersonManager.GetPersonFullInfoAsync(value, token);
        var siblings = await _CurrentProjectProvider
          .Project
          .PersonManager
          .GetSiblings(person, token);

        args.Result = new Tuple<PersonFullInfo, Siblings>(person, siblings);
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

        var (person, siblings) = (Tuple<PersonFullInfo, Siblings>)args.Result;
        _PersonFullInfo = person;
          
        void Add(BiologicalSex biologicalSex, RelativeInfo[] relatives)
        {
          foreach (var relative in relatives)
          {
            if (relative.BiologicalSex != biologicalSex)
            {
              continue;
            }

            var relativeInfoItem = new RelativeInfoItem(
              _PersonFullInfo.BirthDate, relative, _DateFormmater, _RelationshipTypeFormatter, _NameFormmater);
            _Relatives.Add(relativeInfoItem);
          }
        }

        Add(BiologicalSex.Female, PersonManager.Parent(_PersonFullInfo));
        Add(BiologicalSex.Male, PersonManager.Parent(_PersonFullInfo));
        Add(BiologicalSex.Female, PersonManager.AdoptiveParent(person));
        Add(BiologicalSex.Male, PersonManager.AdoptiveParent(person));
        Add(BiologicalSex.Female, siblings.Native);
        Add(BiologicalSex.Male, siblings.Native);
        Add(BiologicalSex.Female, siblings.ByFather);
        Add(BiologicalSex.Male, siblings.ByFather);
        Add(BiologicalSex.Female, siblings.ByMother);
        Add(BiologicalSex.Male, siblings.ByMother);
        Add(BiologicalSex.Female, siblings.Step);
        Add(BiologicalSex.Male, siblings.Step);
        Add(BiologicalSex.Female, siblings.Adoptive);
        Add(BiologicalSex.Male, siblings.Adoptive);
        Add(BiologicalSex.Female, PersonManager.Children(person));
        Add(BiologicalSex.Male, PersonManager.Children(person));
        Add(BiologicalSex.Female, PersonManager.AdoptiveChildren(person));
        Add(BiologicalSex.Male, PersonManager.AdoptiveChildren(person));

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
        Utils.RefreshView(this);
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

      _PersonFullInfo = info;
      Utils.RefreshView(this);
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