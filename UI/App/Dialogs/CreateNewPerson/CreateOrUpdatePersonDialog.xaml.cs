using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Components;
using GT4.UI.App.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace GT4.UI.App.Dialogs;

public partial class CreateOrUpdatePersonDialog : ContentPage
{
  private readonly ServiceProvider _ServiceProvider;
  private readonly ICommand _DialogCommand;
  private readonly string _SaveButtonName;
  private readonly ObservableCollection<ImageSource> _Photos = new();
  private readonly ObservableCollection<NameInfoItem> _Names = new();
  private readonly ObservableCollection<RelativeMemberInfoItem> _Relatives = new();
  private readonly ObservableCollection<BiologicalSexItem> _BiologicalSexes = new();
  private readonly TaskCompletionSource<PersonFullInfo?> _Info = new(null);
  private int? _PersonId;
  private Date? _BirthDate;
  private Date? _DeathDate;
  private BiologicalSexItem? _BiologicalSex;
  private bool _NotReady => _BiologicalSex is null || _BirthDate is null;

  public CreateOrUpdatePersonDialog(PersonFullInfo? person, ServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _DialogCommand = new Command<object>(OnDialogCommand);
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Male, ServiceBuilder.DefaultServices));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Female, ServiceBuilder.DefaultServices));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Unknown, ServiceBuilder.DefaultServices));
    _BiologicalSex = _BiologicalSexes.Where(i => i.Info == person?.BiologicalSex).FirstOrDefault();
    UpdatePersonInformation(person);

    InitializeComponent();
  }

  void UpdatePersonInformation(PersonFullInfo? person)
  {
    if (person is null)
    {
      return;
    }

    _PersonId = person.Id;
    BirthDate = person.BirthDate;
    DeathDate = person.DeathDate;
    if (person.MainPhoto is not null)
    {
      _Photos.Add(ImageUtils.ImageFromBytes(person.MainPhoto.Content));
    }

    var nameFormater = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    foreach (var name in person.Names)
    {
      Names.Add(new NameInfoItem(name, nameFormater));
    }

    foreach (var photo in person.AdditionalPhotos ?? [])
    {
      Photos.Add(ImageUtils.ImageFromBytes(photo.Content));
    }

    if (person.Biography is not null)
    {
      Biography = person.Biography.MimeType switch
      {
        System.Net.Mime.MediaTypeNames.Text.Plain =>
          System.Text.Encoding.UTF8.GetString(person.Biography.Content),

        _ => throw new NotSupportedException($"MIME type '{person.Biography.MimeType}' is not supported yet")
      };
    }

    foreach (var relative in person.Relatives ?? [])
    {
      Relatives.Add(new RelativeMemberInfoItem(relative, _ServiceProvider));
    }
  }

  public ICommand DialogCommand => _DialogCommand;
  public ICollection<ImageSource> Photos => _Photos;
  public ICollection<NameInfoItem> Names => _Names;
  public ICollection<RelativeMemberInfoItem> Relatives => _Relatives;
  public ICollection<BiologicalSexItem> BiologicalSexes => _BiologicalSexes;

  public Date? BirthDate
  {
    get => _BirthDate;
    set
    {
      _BirthDate = value;
      OnPropertyChanged(nameof(BirthDate));
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Date? DeathDate
  {
    get => _DeathDate;
    set
    {
      _DeathDate = value;
      OnPropertyChanged(nameof(DeathDate));
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public BiologicalSexItem? BioSex
  {
    get => _BiologicalSex;
    set
    {
      _BiologicalSex = value;
      OnPropertyChanged(nameof(BioSex));
      OnPropertyChanged(nameof(CreatePersonBtnName));
    }
  }

  public Task<PersonFullInfo?> Info => _Info.Task;
  public string CreatePersonBtnName => _NotReady ? UIStrings.BtnNameCancel : _SaveButtonName;
  public string Biography { get; set; } = string.Empty;

  private void OnCreatePersonCommand()
  {
    if (_NotReady)
    {
      _Info.SetResult(null);
      return;
    }

    var mimeTypeBmp = System.Net.Mime.MediaTypeNames.Image.Bmp;
    var mimeTypePlaintText = System.Net.Mime.MediaTypeNames.Text.Plain;
    var token = _ServiceProvider
      .GetRequiredService<ICancellationTokenProvider>()
      .CreateDbCancellationToken();

    var photos = Task.WhenAll(
      _Photos
      .Select(photo => ImageUtils.ToBytesAsync(photo, token)))
      .Result
      .Where(content => content is not null)
      .Select(content => new Data(Id: TableBase.NonCommitedId, Content: content!, MimeType: mimeTypeBmp, Category: DataCategory.PersonPhoto));
    
    var mainPhoto = photos?.FirstOrDefault();

    var result = new PersonFullInfo(
      Id: _PersonId ?? TableBase.NonCommitedId,
      Names: _Names.Select(n => n.Info).ToArray(),
      MainPhoto: mainPhoto is null ? null : mainPhoto with { Category = DataCategory.PersonMainPhoto },
      BirthDate: _BirthDate!.Value,
      DeathDate: _DeathDate,
      BiologicalSex: _BiologicalSex!.Info,
      AdditionalPhotos: photos?.Skip(1).ToArray(),
      Relatives: _Relatives.Select(relative => relative.Info).ToArray(),
      Biography: new Data(Id: TableBase.NonCommitedId, Content: System.Text.Encoding.UTF8.GetBytes(Biography), mimeTypePlaintText, Category: DataCategory.PersonBio)
    );

    _Info.SetResult(result);
  }

  private async Task OnAddPersonNameAsync()
  {
    if (_BiologicalSex is null)
    {
      // TODO Show Alert
      return;
    }

    var dialog = new SelectNameDialog(
      biologicalSex: _BiologicalSex?.Info ?? BiologicalSex.Unknown,
      serviceProvider: _ServiceProvider
    );

    await Navigation.PushModalAsync(dialog);
    var name = await dialog.Name;
    await Navigation.PopModalAsync();

    var nameFormater = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    if (name is not null)
    {
      _Names.Add(new NameInfoItem(name, nameFormater));
    }
  }

  private async Task OnBirthDateSetupAsync()
  {
    var dialog = new SelectDateDialog(date: BirthDate, serviceProvider: _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var date = await dialog.Info;
    await Navigation.PopModalAsync();

    if (date is not null)
    {
      BirthDate = date;
    }
  }

  private async Task OnDeathDateSetupAsync()
  {
    var dialog = new SelectDateDialog(date: DeathDate, serviceProvider: _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var date = await dialog.Info;
    await Navigation.PopModalAsync();

    if (date is not null)
    {
      DeathDate = date;
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
      case string name when name == "EditBirthDateCommand":
        await OnBirthDateSetupAsync();
        break;
      case string name when name == "EditDeathDateCommand":
        await OnDeathDateSetupAsync();
        break;
      case string name when name == "RemoveDeathDateCommand":
        DeathDate = null;
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
