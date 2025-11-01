using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Components;
using GT4.UI.App.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewPersonDialog : ContentPage
{
  private readonly ServiceProvider _ServiceProvider;
  private readonly ICommand _DialogCommand;
  private readonly string _SaveButtonName;
  private readonly ObservableCollection<ImageSource> _Photos = new();
  private readonly ObservableCollection<NameInfoItem> _Names = new();
  private readonly ObservableCollection<RelativeMemberInfoItem> _Relatives = new();
  private readonly TaskCompletionSource<Person?> _Person = new(null);
  private Date _BirthDate;
  private Date? _DeathDate;
  private BiologicalSex _Sex;
  private bool _NotReady = true;

  public CreateNewPersonDialog(Person? person, ServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _DialogCommand = new Command<object>(OnDialogCommand);
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;

    // TODO just testing
    _Photos.Add(ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync("female_stub.png")));
    _Photos.Add(ImageSource.FromStream(_ => FileSystem.OpenAppPackageFileAsync("male_stub.png")));

    // TODO just testing
    var nameTypeFormatter = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    _Names.Add(new NameInfoItem(new Name(0, "Clark", NameType.FirstName, null), nameTypeFormatter));
    _Names.Add(new NameInfoItem(new Name(0, "Jeremy", NameType.AdditionalName, null), nameTypeFormatter));
    _Names.Add(new NameInfoItem(new Name(0, "Campbell", NameType.LastName, null), nameTypeFormatter));

    // TODO just testing
    _BirthDate = Date.Create(20251029, DateStatus.WellKnown);

    // TODO relatives just testing
    _Relatives.Add(new RelativeMemberInfoItem(new Relative(
      new Person(0, [new Name(0, "Jane", NameType.FirstName, 0)], null, Date.Create(19900000, DateStatus.YearApproximate), null, BiologicalSex.Female),
      RelationshipType.Mother, Date.Create(20050521, DateStatus.WellKnown)), _ServiceProvider));
    _Relatives.Add(new RelativeMemberInfoItem(new Relative(
      new Person(0, [new Name(0, "Doe", NameType.LastName, 0)], null, Date.Create(19951127, DateStatus.DayUnknown), null, BiologicalSex.Male),
      RelationshipType.Father, Date.Create(19850521, DateStatus.YearApproximate)), _ServiceProvider));

    InitializeComponent();
  }

  public ICommand DialogCommand => _DialogCommand;
  public ICollection<ImageSource> Photos => _Photos;
  public ICollection<NameInfoItem> Names => _Names;
  public ICollection<RelativeMemberInfoItem> Relatives => _Relatives;

  public Date BirthDate
  {
    get => _BirthDate;
    set
    {
      _BirthDate = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Date? DeathDate
  {
    get => _DeathDate;
    set
    {
      _DeathDate = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public BiologicalSex Sex
  {
    get => _Sex;
    set
    {
      _Sex = value;
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Task<Person?> Person => _Person.Task;
  public string CreatePersonBtnName => _NotReady ? UIStrings.BtnNameCancel : _SaveButtonName;
  public string Biography { get; set; } = string.Empty;

  private void OnCreatePersonCommand()
  {
    // TODO
    _Person.SetResult(null);
  }

  private async Task OnAddPersonNameAsync()
  {
    var dialog = new SelectNameDialog(biologicalSex: _Sex, serviceProvider: _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var name = await dialog.Name;
    await Navigation.PopModalAsync();

    if (name is not null)
    {
      _Names.Add(new NameInfoItem(name, _ServiceProvider.GetRequiredService<INameTypeFormatter>()));
    }
  }

  private async void OnDialogCommand(object obj)
  {
    switch (obj)
    {
      case string name when name == "CreatePersonCommand":
        OnCreatePersonCommand();
        break;
      case string name when name == "AddNameCommand":
        await OnAddPersonNameAsync();
        break;

      case AdornerCommandParameter adorner when adorner.CommandName == "EditPhotoCommand":
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemovePhotoCommand" && adorner.Element is ImageSource photo:
        _Photos.Remove(photo);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditNameCommand":
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveNameCommand" && adorner.Element is NameInfoItem name:
        _Names.Remove(name);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditRelativeCommand":
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveRelativeCommand" && adorner.Element is RelativeMemberInfoItem relative:
        _Relatives.Remove(relative);
        break;
    }
  }
}