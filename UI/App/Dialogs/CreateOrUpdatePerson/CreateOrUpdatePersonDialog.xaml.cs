using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class CreateOrUpdatePersonDialog : ContentPage
{
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICommand _DialogCommand;
  private readonly string _SaveButtonName;
  private readonly ObservableCollection<PersonDataItem> _Photos = new();
  private readonly ObservableCollection<NameInfoItem> _Names = new();
  private readonly ObservableCollection<RelativeMemberInfoItem> _Relatives = new();
  private readonly ObservableCollection<BiologicalSexItem> _BiologicalSexes = new();
  private readonly TaskCompletionSource<PersonFullInfo?> _Info = new(null);
  private int? _PersonId;
  private Date? _BirthDate;
  private Date? _DeathDate;
  private BiologicalSexItem? _BiologicalSex;
  private PersonDataItem? _Biography;
  private bool _NotReady => _BiologicalSex is null || _BirthDate is null;

  public CreateOrUpdatePersonDialog(PersonFullInfo? person, IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _DialogCommand = new Command<object>(OnDialogCommand);
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;
    var biologicalSexFormatter = _ServiceProvider.GetRequiredService<IBiologicalSexFormatter>();
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Male, biologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Female, biologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Unknown, biologicalSexFormatter));
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
    _BirthDate = person.BirthDate;
    _DeathDate = person.DeathDate;
    var nameFormater = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    foreach (var name in person.Names)
    {
      _Names.Add(new NameInfoItem(name, nameFormater));
    }

    if (person.MainPhoto is not null)
    {
      _Photos.Add(new PersonDataItem(
        data: person.MainPhoto,
        _ServiceProvider.GetRequiredKeyedService<IDataConverter>(person.MainPhoto.Category),
        _ServiceProvider.GetRequiredService<ICancellationTokenProvider>()));
    }

    foreach (var photo in person.AdditionalPhotos)
    {
      _Photos.Add(new PersonDataItem(
        data: photo,
        _ServiceProvider.GetRequiredKeyedService<IDataConverter>(photo.Category),
        _ServiceProvider.GetRequiredService<ICancellationTokenProvider>()));
    }

    _Biography = person.Biography switch
    {
      Data biography =>
        new PersonDataItem(
            data: biography,
            _ServiceProvider.GetRequiredKeyedService<IDataConverter>(DataCategory.PersonBio),
            _ServiceProvider.GetRequiredService<ICancellationTokenProvider>()),

      _ => new PersonDataItem(
            dataCategory: DataCategory.PersonBio,
            _ServiceProvider.GetRequiredKeyedService<IDataConverter>(DataCategory.PersonBio),
            _ServiceProvider.GetRequiredService<ICancellationTokenProvider>())
    };


    foreach (var relative in person.Relatives)
    {
      _Relatives.Add(new RelativeMemberInfoItem(relative, _ServiceProvider));
    }
  }

  public ICommand DialogCommand => _DialogCommand;
  public ICollection<PersonDataItem> Photos => _Photos;
  public ICollection<NameInfoItem> Names => _Names;
  public ICollection<RelativeMemberInfoItem> Relatives => _Relatives;
  public ICollection<BiologicalSexItem> BiologicalSexes => _BiologicalSexes;
  public PersonDataItem? Biography => _Biography;

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

  private void OnCreatePersonCommand()
  {
    if (_NotReady)
    {
      _Info.SetResult(null);
      return;
    }

    var token = _ServiceProvider
      .GetRequiredService<ICancellationTokenProvider>()
      .CreateDbCancellationToken();

    // We do not change the person photo, so we can reuse photo.Info rather than using photo.ToDataAsync()
    var photos = 
      _Photos
      .Select(photo => photo.Info)
      .Where(data => data is not null)
      .Select(data => data!);

    var mainPhoto = photos?.FirstOrDefault();
    var additionalPhotos = photos?.Skip(1).ToArray() ?? [];
    var result = new PersonFullInfo(
      Id: _PersonId ?? TableBase.NonCommitedId,
      Names: _Names.Select(n => n.Info).ToArray(),
      MainPhoto: mainPhoto is null ? null : mainPhoto with { Category = DataCategory.PersonMainPhoto },
      BirthDate: _BirthDate!.Value,
      DeathDate: _DeathDate,
      BiologicalSex: _BiologicalSex!.Info,
      AdditionalPhotos: additionalPhotos,
      Relatives: _Relatives.Select(relative => relative.Info).ToArray(),
      Biography: _Biography?.ToDataAsync().Result);

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
    var dialog = new SelectDateDialog(date: BirthDate, dateFormatter: _ServiceProvider.GetRequiredService<IDateFormatter>());

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
    var dialog = new SelectDateDialog(
      date: DeathDate,
      dateFormatter: _ServiceProvider.GetRequiredService<IDateFormatter>());

    await Navigation.PushModalAsync(dialog);
    var date = await dialog.Info;
    await Navigation.PopModalAsync();

    if (date is not null)
    {
      DeathDate = date;
    }
  }

  private static byte[] FromStream(Stream stream)
  {
    using var ret = new MemoryStream();
    stream.CopyTo(ret);
    return ret.ToArray();
  }

  private async Task OnAddPhotoAsync()
  {
    var pickOptions = new PickOptions
    {
      PickerTitle = UIStrings.FileDialogSelectPictures,
      FileTypes = FilePickerFileType.Images
    };
    var result = await FilePicker.Default.PickMultipleAsync(pickOptions);
    if (result is null)
    {
      return;
    }

    IEnumerable<Stream>? streams = null;
    try
    {
      var token = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>().CreateShortOperationCancellationToken();
      var filesContent = result.Select(file => (Stream: file.OpenReadAsync(), MimeType: file.ContentType));
      streams = await Task.WhenAll(filesContent.Select(file => file.Stream));
      var photoAssets = filesContent.Select(content =>
          new Data(
            Id: TableBase.NonCommitedId,
            Content: FromStream(content.Stream.Result),
            MimeType: content.MimeType,
            Category: default));

      foreach (var photoAsset in photoAssets)
      {
        var category = Photos.Count() == 0 ? DataCategory.PersonMainPhoto : DataCategory.PersonPhoto;

        Photos.Add(new PersonDataItem(
          data: photoAsset with { Category = category },
          dataConverter: _ServiceProvider.GetRequiredKeyedService<IDataConverter>(category),
          cancellationTokenProvider: _ServiceProvider.GetRequiredService<ICancellationTokenProvider>()));
      }
    }
    finally
    {
      foreach (var stream in streams ?? [])
      {
        stream.Close();
        stream.Dispose();
      }
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
      case string name when name == "AddPhotoCommand":
        await OnAddPhotoAsync();
        break;

      case AdornerCommandParameter adorner when adorner.CommandName == "EditPhotoCommand":
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemovePhotoCommand" && adorner.Element is PersonDataItem photo:
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
