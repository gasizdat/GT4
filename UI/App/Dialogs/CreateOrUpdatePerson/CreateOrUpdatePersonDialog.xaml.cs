using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Converters;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class CreateOrUpdatePersonDialog : ContentPage
{
  public record class Factory(
    ICancellationTokenProvider CancellationTokenProvider,
    IBiologicalSexFormatter BiologicalSexFormatter,
    INameTypeFormatter NameTypeFormatter,
    INameFormatter NameFormatter,
    IDateFormatter DateFormatter,
    IComparer<PersonInfo> PersonInfoComparer,
    IAlertService AlertService,
    DataConverterResolver DataConverterFactory,
    SelectNameDialog.Factory SelectNameDialogFactory,
    SelectRelativesDialog.Factory SelectRelativesDialogFactory,
    SelectPersonDialog.Factory SelectPersonDialogFactory)
  {
    public CreateOrUpdatePersonDialog Create(PersonFullInfo? person) =>
      new CreateOrUpdatePersonDialog(this, person);
  }

  private readonly Factory _Factory;
  private readonly ICommand _DialogCommand;
  private readonly string _SaveButtonName;
  private readonly ObservableCollection<PersonDataItem> _Photos = new();
  private readonly ObservableCollection<PersonDataItem> _Attachments = new();
  private readonly ObservableCollection<NameInfoItem> _Names = new();
  private readonly ObservableCollection<RelativeInfo> _Relatives = new();
  private readonly ObservableCollection<BiologicalSexItem> _BiologicalSexes = new();
  private readonly TaskCompletionSource<PersonFullInfo?> _Info = new(null);
  private int? _PersonId;
  private Date? _BirthDate;
  private Date? _DeathDate;
  private BiologicalSexItem? _BiologicalSex;
  private PersonDataItem? _Biography;
  private Data? _GedcomData;
  private bool _IsModified;
  private bool _NotReady => _BiologicalSex is null || _BirthDate is null || !_IsModified;

  protected CreateOrUpdatePersonDialog(Factory factory, PersonFullInfo? person)
  {
    _Factory = factory;
    _DialogCommand = new SafeCommand(OnDialogCommand, _Factory.AlertService);
    _SaveButtonName = person is null ? UIStrings.BtnNameCreateFamilyPerson : UIStrings.BtnNameUpdateFamilyPerson;
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Male, _Factory.BiologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Female, _Factory.BiologicalSexFormatter));
    _BiologicalSexes.Add(new BiologicalSexItem(BiologicalSex.Unknown, _Factory.BiologicalSexFormatter));
    _BiologicalSex = _BiologicalSexes.FirstOrDefault(i => i.Info == person?.BiologicalSex);

    UpdatePersonInformation(person);

    _Names.CollectionChanged += (_, _) => OnPropertyChanged(nameof(PersonFullName));

    InitializeComponent();
    IsModified = false;
  }

  private PersonDataItem GetPersonData(Data data, DataCategory dataCategory)
  {
    var ret = new PersonDataItem(
      data: data,
      _Factory.DataConverterFactory(dataCategory),
      _Factory.CancellationTokenProvider,
      _Factory.AlertService);

    return ret;
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
      _GedcomData = person.GedcomData;
      var names = person
        .Names
        .Select(name => new NameInfoItem(name, _Factory.NameTypeFormatter))
        .GroupBy(item => item.Info.Type & NameType.NoDeclension)
        .OrderBy(group => group.Key)
        .SelectMany(item => item);

      foreach (var name in names)
      {
        _Names.Add(name);
      }

      if (person.MainPhoto is not null)
      {
        _Photos.Add(GetPersonData(person.MainPhoto, person.MainPhoto.Category));
      }

      foreach (var photo in person.AdditionalPhotos)
      {
        _Photos.Add(GetPersonData(photo, photo.Category));
      }

      foreach (var attachment in person.Attachments)
      {
        _Attachments.Add(GetPersonData(attachment, DataCategory.PersonAttachment));
      }

      _Biography = person.Biography switch
      {
        Data biography => GetPersonData(biography, DataCategory.PersonBio),

        _ => new PersonDataItem(
              dataCategory: DataCategory.PersonBio,
              _Factory.DataConverterFactory(DataCategory.PersonBio),
              _Factory.CancellationTokenProvider,
              _Factory.AlertService)
      };
      _Biography.PropertyChanged += (_, _) => IsModified = _Biography.IsModified;

      var relatives = person
        .RelativeInfos
        .OrderBy(item => item, _Factory.PersonInfoComparer);
      foreach (var relative in relatives)
      {
        _Relatives.Add(relative);
      }
    }
    catch (Exception ex)
    {
      _ = _Factory.AlertService.ShowErrorAsync(ex);
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

  public ICollection<PersonDataItem> Attachments => _Attachments;

  public ICollection<NameInfoItem> Names => _Names;

  public ICollection<RelativeInfo> Relatives => _Relatives;

  public ICollection<BiologicalSexItem> BiologicalSexes => _BiologicalSexes;

  public PersonDataItem? Biography => _Biography;

  public string PersonFullName
  {
    get
    {
      var dummyPersonInfo = PersonFullInfo.Empty with { Names = _Names.Select(item => item.Info).ToArray() };
      return _Factory.NameFormatter.ToString(dummyPersonInfo, NameFormat.FullPersonName);
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

  private async Task OnCreatePersonCommandAsync()
  {
    if (_NotReady)
    {
      _Info.SetResult(null);
      return;
    }

    var photos = (await Task.WhenAll(_Photos.Select(photo => photo.ToDataAsync())))
      .Where(data => data is not null)
      .Select(data => data!);
    var attachments = (await Task.WhenAll(_Attachments.Select(attachment => attachment.ToDataAsync())))
      .Where(data => data is not null)
      .Select(data => data!)
      .ToArray();

    var mainPhoto = photos?.FirstOrDefault();
    var additionalPhotos = photos?
      .Skip(1)
      .Select(p => p with { Category = p.Category.AsAdditionalPhoto() })
      .ToArray() ?? [];
    var person = new Person(
      Id: _PersonId ?? ElementId.NonCommittedId,
      BirthDate: _BirthDate!.Value,
      DeathDate: _DeathDate,
      BiologicalSex: _BiologicalSex!.Info);
    var result = new PersonFullInfo(
      person: person,
      names: _Names.Select(n => n.Info).ToArray(),
      additionalPhotos: additionalPhotos,
      relativeInfos: [.. _Relatives],
      // .AsMainPhoto()/.AsAdditionalPhoto() preserve tagged-vs-plain while fixing the main/additional
      // bucket -- position 0 is authoritative regardless of each photo's category coming in, since
      // reordering (MovePhotoToLeft/Right) doesn't itself update DataCategory.
      mainPhoto: mainPhoto is null ? null : mainPhoto with { Category = mainPhoto.Category.AsMainPhoto() },
      biography: _Biography?.ToDataAsync().Result,
      gedcomData: _GedcomData,
      attachments: attachments);

    _Info.SetResult(result);
  }

  protected async Task OnAddPersonNameAsync()
  {
    var biologicalSex = _BiologicalSex?.Info ?? BiologicalSex.Unknown;
    if (biologicalSex == BiologicalSex.Unknown)
    {
      var message = string.Format(UIStrings.AlertTextUnableToAddNameForTheSexSelected_1,
        _Factory.BiologicalSexFormatter.ToString(_BiologicalSex?.Info));
      await _Factory.AlertService.ShowWarningAsync(message);
      return;
    }

    var alreadyInFamily = Names.SingleOrDefault(n => n.Info.Type.HasFlag(NameType.FamilyName)) is not null;
    NameType[] familyName = alreadyInFamily ? [] : [NameType.FamilyName];

    var dialog = _Factory.SelectNameDialogFactory.Create(
      biologicalSex,
      [NameType.FirstName, NameType.Patronymic, NameType.LastName, .. familyName]);

    await Navigation.PushModalAsync(dialog);
    var name = await dialog.Name;
    await Navigation.PopModalAsync();

    if (name is not null)
    {
      var lastNameWithTheSameType = _Names.LastOrDefault(item => item?.Info.Type == name.Type, null);
      var index = lastNameWithTheSameType is null ? -1 : _Names.IndexOf(lastNameWithTheSameType);
      var item = new NameInfoItem(name, _Factory.NameTypeFormatter);
      _Names.Insert(index + 1, item);

      IsModified = true;
    }
  }

  protected async Task OnEditPersonNameAsync(NameInfoItem nameInfoItem)
  {
    var dialog = _Factory.SelectNameDialogFactory.Create(
      _BiologicalSex?.Info ?? BiologicalSex.Unknown,
      [nameInfoItem.Info.Type]);

    await Navigation.PushModalAsync(dialog);
    var name = await dialog.Name;
    await Navigation.PopModalAsync();

    if (name is not null)
    {
      var index = _Names.IndexOf(nameInfoItem);
      _Names[index] = new NameInfoItem(name, _Factory.NameTypeFormatter);
    }
  }

  private async Task OnBirthDateSetupAsync()
  {
    var dialog = new SelectDateDialog(
      date: BirthDate,
      dateFormatter: _Factory.DateFormatter,
      alertService: _Factory.AlertService);

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
      dateFormatter: _Factory.DateFormatter,
      alertService: _Factory.AlertService);

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
      var files = await FilePicker.Default.PickMultipleAsync(pickOptions);
      results = files?.Where(f => f is not null).Select(r => r!);
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
            Id: ElementId.NonCommittedId,
            Content: FromStream(content.Stream.Result),
            MimeType: content.MimeType,
            Category: default));

      foreach (var photoAsset in photoAssets)
      {
        var category = _Photos.Count() == 0 ? DataCategory.PersonMainPhoto : DataCategory.PersonPhoto;
        var item = GetPersonData(data: photoAsset with { Category = category }, category);
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

  private async Task OnAddOrUpdateAttachmentAsync(PersonDataItem? attachment)
  {
    var pickOptions = new PickOptions { PickerTitle = UIStrings.FileDialogSelectAttachment };
    IEnumerable<FileResult>? results;
    if (attachment is null)
    {
      var files = await FilePicker.Default.PickMultipleAsync(pickOptions);
      results = files?.Where(f => f is not null).Select(r => r!);
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

    var converter = _Factory.DataConverterFactory(DataCategory.PersonAttachment);
    IEnumerable<Stream>? streams = null;
    try
    {
      var filesContent = results.Select(file => (Stream: file.OpenReadAsync(), file.FileName, MimeType: file.ContentType)).ToArray();
      streams = await Task.WhenAll(filesContent.Select(file => file.Stream));

      foreach (var content in filesContent)
      {
        using var token = _Factory.CancellationTokenProvider.CreateShortOperationCancellationToken();
        var pick = new AttachmentPick(FromStream(content.Stream.Result), content.FileName, content.MimeType);
        var attachmentAsset = await converter.FromObjectAsync(pick, token);
        if (attachmentAsset is null)
        {
          continue;
        }

        var item = GetPersonData(attachmentAsset, DataCategory.PersonAttachment);
        if (attachment is not null)
        {
          _Attachments[_Attachments.IndexOf(attachment)] = item;
        }
        else
        {
          _Attachments.Add(item);
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
    var dialog = _Factory.SelectRelativesDialogFactory.Create(_BiologicalSex?.Info, [.. Relatives]);

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

  protected async Task OnInsertLinkAsync()
  {
    var dialog = _Factory.SelectPersonDialogFactory.Create();

    await Navigation.PushModalAsync(dialog);
    var person = await dialog.Info;
    await Navigation.PopModalAsync();

    if (person is not null)
    {
      var displayName = _Factory.NameFormatter.ToString(person, NameFormat.CommonPersonName);
      BiographyEditor.InsertLink(displayName, person.Id);
    }
  }

  private async Task OnEditRelationshipAsync(RelativeInfo relative)
  {
    var dialog = new SelectDateDialog(
      date: relative.Date,
      dateFormatter: _Factory.DateFormatter,
      alertService: _Factory.AlertService);

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

  private async Task OnDialogCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "CreatePersonCommand":
        await OnCreatePersonCommandAsync();
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
      case string commandName when commandName == "AddAttachmentCommand":
        await OnAddOrUpdateAttachmentAsync(null);
        break;
      case string commandName when commandName == "AddRelationship":
        await OnAddRelationshipAsync();
        break;
      case string commandName when commandName == "InsertPersonLinkCommand":
        await OnInsertLinkAsync();
        break;
      case string commandName when commandName == "UndefinedBirthDateCommand":
        SetUndefinedBirthDate();
        break;
      case string commandName when commandName == "UndefinedDeathDateCommand":
        SetUndefinedDeathDate();
        break;

      case AdornerCommandParameter adorner when adorner.CommandName == "EditPhotoCommand" && adorner.Element is PersonDataItem photo:
        await OnAddOrUpdatePhotoAsync(photo);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemovePhotoCommand" && adorner.Element is PersonDataItem photo:
        _Photos.Remove(photo);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MovePhotoToLeftCommand" && adorner.Element is PersonDataItem photo:
        MoveItem(_Photos, photo, -1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MovePhotoToRightCommand" && adorner.Element is PersonDataItem photo:
        MoveItem(_Photos, photo, 1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditAttachmentCommand" && adorner.Element is PersonDataItem attachment:
        await OnAddOrUpdateAttachmentAsync(attachment);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveAttachmentCommand" && adorner.Element is PersonDataItem attachment:
        _Attachments.Remove(attachment);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveAttachmentUpCommand" && adorner.Element is PersonDataItem attachment:
        MoveItem(_Attachments, attachment, -1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveAttachmentDownCommand" && adorner.Element is PersonDataItem attachment:
        MoveItem(_Attachments, attachment, 1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditNameCommand" && adorner.Element is NameInfoItem name:
        await OnEditPersonNameAsync(name);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveNameCommand" && adorner.Element is NameInfoItem { CanBeRemoved: true } name:
        _Names.Remove(name);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveNameUpCommand" && adorner.Element is NameInfoItem name:
        MoveItem(_Names, name, -1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveNameDownCommand" && adorner.Element is NameInfoItem name:
        MoveItem(_Names, name, 1);
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

  private void MoveItem<T>(ObservableCollection<T> collection, T name, int dIndex)
  {
    var oldIndex = collection.IndexOf(name);
    var newIndex = oldIndex + dIndex;

    if (newIndex < 0 || newIndex >= collection.Count)
    {
      throw new ApplicationException(UIStrings.ErrorTheBoundIsReached);
    }

    collection.Move(oldIndex, newIndex);

    IsModified = true;
  }

  private void SetUndefinedBirthDate()
  {
    BirthDate = Date.Create(0, DateStatus.Unknown);
  }

  private void SetUndefinedDeathDate()
  {
    DeathDate = Date.Create(0, DateStatus.Unknown);
  }
}
