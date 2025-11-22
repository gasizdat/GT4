using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Converters;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class CreateOrUpdatePersonDialog : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IRelationshipTypeFormatter _RelationshipTypeFormatter;
  private readonly IBiologicalSexFormatter _BiologicalSexFormatter;
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly IComparer<PersonInfoItem> _PersonComparer;
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
  private bool _IsModified;
  private bool _NotReady => _BiologicalSex is null || _BirthDate is null || !_IsModified;

  public CreateOrUpdatePersonDialog(PersonFullInfo? person, IServiceProvider serviceProvider)
  {
    _ServiceProvider = serviceProvider;
    _CancellationTokenProvider = _ServiceProvider.GetRequiredService<ICancellationTokenProvider>();
    _RelationshipTypeFormatter = _ServiceProvider.GetRequiredService<IRelationshipTypeFormatter>();
    _BiologicalSexFormatter = _ServiceProvider.GetRequiredService<IBiologicalSexFormatter>();
    _NameTypeFormatter = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    _NameFormatter = _ServiceProvider.GetRequiredService<INameFormatter>();
    _DateFormatter = _ServiceProvider.GetRequiredService<IDateFormatter>();
    _PersonComparer = _ServiceProvider.GetRequiredService<IComparer<PersonInfoItem>>();
    _DialogCommand = new Command<object>(OnDialogCommand);
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Male, _BiologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Female, _BiologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Unknown, _BiologicalSexFormatter));
    _BiologicalSex = _BiologicalSexes.Where(i => i.Info == person?.BiologicalSex).FirstOrDefault();
    UpdatePersonInformation(person);

    InitializeComponent();
    IsModified = false;
  }

  private void UpdatePersonInformation(PersonFullInfo? person)
  {
    if (person is null)
    {
      return;
    }

    try
    {
      _PersonId = person.Id;
      _BirthDate = person.BirthDate;
      _DeathDate = person.DeathDate;
      foreach (var name in person.Names)
      {
        _Names.Add(new NameInfoItem(name, _NameTypeFormatter));
      }

      if (person.MainPhoto is not null)
      {
        _Photos.Add(new PersonDataItem(
          data: person.MainPhoto,
          _ServiceProvider.GetRequiredKeyedService<IDataConverter>(person.MainPhoto.Category),
          _CancellationTokenProvider));
      }

      foreach (var photo in person.AdditionalPhotos)
      {
        _Photos.Add(new PersonDataItem(
          data: photo,
          _ServiceProvider.GetRequiredKeyedService<IDataConverter>(photo.Category),
          _CancellationTokenProvider));
      }

      _Biography = person.Biography switch
      {
        Data biography =>
          new PersonDataItem(
              data: biography,
              _ServiceProvider.GetRequiredKeyedService<IDataConverter>(DataCategory.PersonBio),
              _CancellationTokenProvider),

        _ => new PersonDataItem(
              dataCategory: DataCategory.PersonBio,
              _ServiceProvider.GetRequiredKeyedService<IDataConverter>(DataCategory.PersonBio),
              _CancellationTokenProvider)
      };

      var relativeItems = person
        .RelativeInfos
        .Select(relativeInfo => new RelativeMemberInfoItem(
          person.BirthDate,
          relativeInfo,
          _DateFormatter,
          _RelationshipTypeFormatter,
          _NameFormatter))
        .OrderBy(item => item, _PersonComparer);

      foreach (var item in relativeItems)
      {
        _Relatives.Add(item);
      }
    }
    catch (Exception ex)
    {
      PageAlert.ShowError(ex);
    }
  }

  private bool IsModified
  {
    set
    {
      _IsModified = value;
      OnPropertyChanged(nameof(DialogButtonName));
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
      IsModified = true;
      OnPropertyChanged(nameof(BirthDate));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public Date? DeathDate
  {
    get => _DeathDate;
    set
    {
      _DeathDate = value;
      IsModified = true;
      OnPropertyChanged(nameof(DeathDate));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public BiologicalSexItem? BioSex
  {
    get => _BiologicalSex;
    set
    {
      _BiologicalSex = value;
      IsModified = true;
      OnPropertyChanged(nameof(BioSex));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public Task<PersonFullInfo?> Info => _Info.Task;

  public string DialogButtonName => _NotReady ? UIStrings.BtnNameCancel : _SaveButtonName;

  private void OnCreatePersonCommand()
  {
    if (_NotReady)
    {
      _Info.SetResult(null);
      return;
    }

    var token = _CancellationTokenProvider.CreateDbCancellationToken();

    // We do not change the person photo, so we can reuse photo.Info rather than using photo.ToDataAsync()
    var photos =
      _Photos
      .Select(photo => photo.Info)
      .Where(data => data is not null)
      .Select(data => data!);

    var mainPhoto = photos?.FirstOrDefault();
    var additionalPhotos = photos?.Skip(1).ToArray() ?? [];
    var person = new Person(
      Id: _PersonId ?? TableBase.NonCommitedId,
      BirthDate: _BirthDate!.Value,
      DeathDate: _DeathDate,
      BiologicalSex: _BiologicalSex!.Info);
    var result = new PersonFullInfo(
      person: person,
      names: _Names.Select(n => n.Info).ToArray(),
      additionalPhotos: additionalPhotos,
      relativeInfos: _Relatives.Select(relative => relative.RelativeInfo).ToArray(),
      mainPhoto: mainPhoto is null ? null : mainPhoto with { Category = DataCategory.PersonMainPhoto },
      biography: _Biography?.ToDataAsync().Result);

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

    if (name is not null)
    {
      _Names.Add(new NameInfoItem(name, _NameTypeFormatter));
      IsModified = true;
    }
  }

  private async Task OnBirthDateSetupAsync()
  {
    var dialog = new SelectDateDialog(date: BirthDate, dateFormatter: _DateFormatter);

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
      dateFormatter: _DateFormatter);

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
      var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
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
          cancellationTokenProvider: _CancellationTokenProvider));
      }

      IsModified = true;
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

  private async Task OnAddRelationshipAsync()
  {
    var existingRelatives = _Relatives
      .Select(item => item.RelativeInfo)
      .ToArray();
    var dialog = new SelectRelativesDialog(
      biologicalSex: _BiologicalSex?.Info,
      existingRelatives: existingRelatives,
      _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var result = await dialog.Info;
    await Navigation.PopModalAsync();

    if (result?.Length > 0)
    {
      var relatives = result
        .Select(person =>
        {
          var relativeInfo = new RelativeInfo(
            person.Info, 
            dialog.RelType.Info, 
            dialog.RelationshipDate, 
            true);
          return relativeInfo;
        })
        .Select(relativeInfo => new RelativeMemberInfoItem(
          _BirthDate.GetValueOrDefault(), 
          relativeInfo, 
          _DateFormatter, 
          _RelationshipTypeFormatter, 
          _NameFormatter));
      foreach (var relative in relatives)
      {
        _Relatives.Add(relative);
      }
      IsModified = true;
    }
  }

  private async Task OnEditRelationshipAsync(RelativeMemberInfoItem relative)
  {
    var dialog = new SelectDateDialog(
      date: relative.RelativeInfo.Date,
      dateFormatter: _DateFormatter);

    await Navigation.PushModalAsync(dialog);
    var date = await dialog.Info;
    await Navigation.PopModalAsync();

    if (date is not null)
    {
      var insertIndex = _Relatives.IndexOf(relative);
      _Relatives.Remove(relative);
      var newRelative = new RelativeMemberInfoItem(
        _BirthDate.GetValueOrDefault(),
        relativeInfo: relative.RelativeInfo with { Date = date },
        _DateFormatter,
        _RelationshipTypeFormatter,
        _NameFormatter);
      _Relatives.Insert(insertIndex, newRelative);
      IsModified = true;
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
      case string name when name == "AddRelationship":
        await OnAddRelationshipAsync();
        break;

      case AdornerCommandParameter adorner when adorner.CommandName == "EditPhotoCommand":
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemovePhotoCommand" && adorner.Element is PersonDataItem photo:
        _Photos.Remove(photo);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditNameCommand":
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveNameCommand" && adorner.Element is NameInfoItem name:
        _Names.Remove(name);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditRelativeCommand" && adorner.Element is RelativeMemberInfoItem relative:
        await OnEditRelationshipAsync(relative);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveRelativeCommand" && adorner.Element is RelativeMemberInfoItem relative:
        _Relatives.Remove(relative);
        IsModified = true;
        break;
    }
  }
}
