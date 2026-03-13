using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class CreateOrUpdatePersonDialog : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IBiologicalSexFormatter _BiologicalSexFormatter;
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IServiceProvider _ServiceProvider;
  private readonly ICommand _DialogCommand;
  private readonly string _SaveButtonName;
  private readonly ObservableCollection<PersonDataItem> _Photos = new();
  private readonly ObservableCollection<NameInfoItem> _Names = new();
  private readonly ObservableCollection<RelativeInfo> _Relatives = new();
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
    _BiologicalSexFormatter = _ServiceProvider.GetRequiredService<IBiologicalSexFormatter>();
    _NameTypeFormatter = _ServiceProvider.GetRequiredService<INameTypeFormatter>();
    _NameFormatter = _ServiceProvider.GetRequiredService<INameFormatter>();
    _DateFormatter = _ServiceProvider.GetRequiredService<IDateFormatter>();
    _PersonInfoComparer = _ServiceProvider.GetRequiredService<IComparer<PersonInfo>>();
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
      var names = person
        .Names
        .Select(name => new NameInfoItem(name, _NameTypeFormatter))
        .GroupBy(item => item.Info.Type & NameType.NoDeclension)
        .OrderBy(group => group.Key)
        .SelectMany(item => item);

      foreach (var name in names)
      {
        _Names.Add(name);
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
      _Biography.PropertyChanged += (_, _) => IsModified = _Biography.IsModified;

      var relatives = person
        .RelativeInfos
        .OrderBy(item => item, _PersonInfoComparer);
      foreach (var relative in relatives)
      {
        _Relatives.Add(relative);
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

  public ICollection<RelativeInfo> Relatives => _Relatives;

  public ICollection<BiologicalSexItem> BiologicalSexes => _BiologicalSexes;

  public PersonDataItem? Biography => _Biography;

  public string PersonFullName
  {
    get
    {
      var dummyPersonInfo = PersonFullInfo.Empty with { Names = _Names.Select(item => item.Info).ToArray() };
      return _NameFormatter.ToString(dummyPersonInfo, NameFormat.FullPersonName);
    }
  }

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
      relativeInfos: [.._Relatives],
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
      var lastNameWithTheSameType = _Names.LastOrDefault(item => item?.Info.Type == name.Type, null);
      var index = lastNameWithTheSameType is null ? -1 : _Names.IndexOf(lastNameWithTheSameType);
      var item = new NameInfoItem(name, _NameTypeFormatter);
      _Names.Insert(index + 1, item);

      OnPropertyChanged(nameof(PersonFullName));
      IsModified = true;
    }
  }

  private async Task OnEditPersonNameAsync(NameInfoItem nameInfoItem)
  {
    var dialog = new SelectNameDialog(
      biologicalSex: _BiologicalSex?.Info ?? BiologicalSex.Unknown,
      serviceProvider: _ServiceProvider
    );

    await Navigation.PushModalAsync(dialog);
    var name = await dialog.Name;
    await Navigation.PopModalAsync();

    if (name is not null)
    {
      var index = _Names.IndexOf(nameInfoItem);
      _Names[index] = new NameInfoItem(name, _NameTypeFormatter);
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

  private async Task OnAddOrUpdatePhotoAsync(PersonDataItem? photo)
  {
    var pickOptions = new PickOptions
    {
      PickerTitle = UIStrings.FileDialogSelectPictures,
      FileTypes = FilePickerFileType.Images
    };
    IEnumerable<FileResult>? results;
    if (photo is null)
    {
      results = await FilePicker.Default.PickMultipleAsync(pickOptions);
    }
    else
    {
      var result = await FilePicker.Default.PickAsync(pickOptions);
      results = result is null ? null : [result];
    }

    if (results is null)
    {
      return;
    }

    IEnumerable<Stream>? streams = null;
    try
    {
      var filesContent = results.Select(file => (Stream: file.OpenReadAsync(), MimeType: file.ContentType));
      streams = await Task.WhenAll(filesContent.Select(file => file.Stream));
      var photoAssets = filesContent.Select(content =>
          new Data(
            Id: TableBase.NonCommitedId,
            Content: FromStream(content.Stream.Result),
            MimeType: content.MimeType,
            Category: default));

      foreach (var photoAsset in photoAssets)
      {
        var category = _Photos.Count() == 0 ? DataCategory.PersonMainPhoto : DataCategory.PersonPhoto;
        var item = new PersonDataItem(
            data: photoAsset with { Category = category },
            dataConverter: _ServiceProvider.GetRequiredKeyedService<IDataConverter>(category),
            cancellationTokenProvider: _CancellationTokenProvider);
        if (photo is not null)
        {
          _Photos[_Photos.IndexOf(photo)] = item;
        }
        else
        {
          _Photos.Add(item);
        }
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
    var dialog = new SelectRelativesDialog(
      biologicalSex: _BiologicalSex?.Info,
      existingRelatives: [..Relatives],
      _ServiceProvider);

    await Navigation.PushModalAsync(dialog);
    var result = await dialog.Info;
    await Navigation.PopModalAsync();

    if (result?.Length > 0)
    {
      foreach (var relative in result)
      {
        _Relatives.Add(relative);
      }
      IsModified = true;
    }
  }

  private async Task OnEditRelationshipAsync(RelativeInfo relative)
  {
    var dialog = new SelectDateDialog(
      date: relative.Date,
      dateFormatter: _DateFormatter);

    await Navigation.PushModalAsync(dialog);
    var date = await dialog.Info;
    await Navigation.PopModalAsync();

    if (date is not null)
    {
      var insertIndex = _Relatives.IndexOf(relative);
      _Relatives.RemoveAt(insertIndex);
      _Relatives.Insert(insertIndex, relative with { Date = date });
      IsModified = true;
    }
  }

  private async void OnDialogCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "CreatePersonCommand":
        OnCreatePersonCommand();
        break;
      case string commandName when commandName == "AddNameCommand":
        await OnAddPersonNameAsync();
        break;
      case string commandName when commandName == "EditBirthDateCommand":
        await OnBirthDateSetupAsync();
        break;
      case string commandName when commandName == "EditDeathDateCommand":
        await OnDeathDateSetupAsync();
        break;
      case string commandName when commandName == "RemoveDeathDateCommand":
        DeathDate = null;
        break;
      case string commandName when commandName == "AddPhotoCommand":
        await OnAddOrUpdatePhotoAsync(null);
        break;
      case string commandName when commandName == "AddRelationship":
        await OnAddRelationshipAsync();
        break;

      case AdornerCommandParameter adorner when adorner.CommandName == "EditPhotoCommand" && adorner.Element is PersonDataItem photo:
        await OnAddOrUpdatePhotoAsync(photo);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemovePhotoCommand" && adorner.Element is PersonDataItem photo:
        _Photos.Remove(photo);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditNameCommand" && adorner.Element is NameInfoItem name:
        await OnEditPersonNameAsync(name);
        OnPropertyChanged(nameof(PersonFullName));
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveNameCommand" && adorner.Element is NameInfoItem name:
        _Names.Remove(name);
        OnPropertyChanged(nameof(PersonFullName));
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditRelativeCommand" && adorner.Element is RelativeInfo relative:
        await OnEditRelationshipAsync(relative);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveRelativeCommand" && adorner.Element is RelativeInfo relative:
        _Relatives.Remove(relative);
        IsModified = true;
        break;
    }
  }
}
