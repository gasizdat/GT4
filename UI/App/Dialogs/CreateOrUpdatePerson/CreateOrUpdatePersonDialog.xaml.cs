using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Converters;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
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
    DataConverterResolver DataConverterResolver,
    [FromKeyedServices(DataCategory.PersonBio)] IDataConverter PersonBioConverter,
    [FromKeyedServices(DataCategory.PersonAttachment)] IDataConverter PersonAttachmentConverter,
    SelectNameDialog.Factory SelectNameDialogFactory,
    SelectRelativesDialog.Factory SelectRelativesDialogFactory,
    SelectPersonDialog.Factory SelectPersonDialogFactory,
    SelectMediaDialog.Factory SelectMediaDialogFactory,
    InlineMediaProvider MediaProvider)
  {
    public CreateOrUpdatePersonDialog Create(PersonFullInfo? person) =>
      new CreateOrUpdatePersonDialog(this, person);
  }

  private readonly Factory _Factory;
  private readonly InlineMediaResolver _MediaResolver;
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
    _MediaResolver = factory.MediaProvider.ResolveAsync;
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
    var converter = _Factory.DataConverterResolver(dataCategory);
    var ret = new PersonDataItem(
      data: data,
      converter,
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
              _Factory.PersonBioConverter,
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

  public InlineMediaResolver MediaResolver => _MediaResolver;

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

    var photos = await PersonDataItem.ToDataAsync(_Photos);
    var attachments = await PersonDataItem.ToDataAsync(_Attachments);

    var mainPhoto = photos.FirstOrDefault();
    var additionalPhotos = photos
      .Skip(1)
      .Select(p => p with { Category = p.Category.AsAdditionalPhoto() })
      .ToArray();
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

  private async Task OnAddOrUpdatePhotoAsync(PersonDataItem? photo)
  {
    var pickOptions = new PickOptions
    {
      PickerTitle = UIStrings.FileDialogSelectPictures,
      FileTypes = FilePickerFileType.Images
    };
    var results = await FilePickUtils.PickFilesAsync(pickOptions, allowMultiple: photo is null);
    if (results is null)
    {
      return;
    }

    var photoAssets = results.Select(file =>
        new Data(
          Id: ElementId.NonCommittedId,
          Content: file.Content,
          MimeType: file.MimeType,
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

  private async Task OnAddOrUpdateAttachmentAsync(PersonDataItem? attachment)
  {
    var pickOptions = new PickOptions { PickerTitle = UIStrings.FileDialogSelectAttachment };
    var results = await FilePickUtils.PickFilesAsync(pickOptions, allowMultiple: attachment is null);
    if (results is null)
    {
      return;
    }

    var converter = _Factory.PersonAttachmentConverter;
    using var token = _Factory.CancellationTokenProvider.CreateShortOperationCancellationToken();
    foreach (var file in results)
    {
      var pick = new AttachmentPick(file.Content, file.FileName, file.MimeType);
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

  // Only a committed photo/attachment has a stable id a stored biography link can target -- one just
  // picked in this same edit session has none yet (see PersonDataItem.ToDataAsync).
  protected async Task OnInsertMediaLinkAsync()
  {
    using var token = _Factory.CancellationTokenProvider.CreateShortOperationCancellationToken();
    var photos = _Photos.Select(photo => photo.Info).Where(data => data.Id != ElementId.NonCommittedId);
    var attachments = _Attachments.Select(attachment => attachment.Info).Where(data => data.Id != ElementId.NonCommittedId);

    var photoItems = await Task.WhenAll(photos.Select((data, index) =>
      ToMediaLinkItemAsync(data, string.Format(UIStrings.MediaLinkPhotoName_1, index + 1), token)));
    var attachmentItems = await Task.WhenAll(attachments.Select(data => ToMediaLinkItemAsync(data, string.Empty, token)));

    var dialog = _Factory.SelectMediaDialogFactory.Create([.. photoItems, .. attachmentItems]);
    await Navigation.PushModalAsync(dialog);
    var picked = await dialog.Info;
    await Navigation.PopModalAsync();

    if (picked is not null)
    {
      if (picked.IsInlineImage)
      {
        BiographyEditor.InsertMediaLink(picked.DisplayName, picked.Id);
      }
      else
        BiographyEditor.InsertAttachmentLink(picked.DisplayName, picked.Id);
    }
  }

  // IsInlineImage must be the same predicate the media resolver filters by: offering something the
  // resolver rejects would insert an embed that renders as nothing instead of a working link.
  private async Task<MediaLinkItem> ToMediaLinkItemAsync(Data data, string fallbackName, CancellationToken token)
  {
    var content = await _Factory.DataConverterResolver(data.Category).ToObjectAsync(data, token);
    var displayName = content switch
    {
      PhotoInfo photo when !string.IsNullOrWhiteSpace(photo.Caption) => photo.Caption,
      AttachmentInfo attachment => attachment.DisplayName,
      _ => fallbackName
    };
    var isInlineImage = data.IsInlineImage();
    return new MediaLinkItem(data.Id, isInlineImage, displayName);
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
      case string commandName when commandName == "InsertMediaLinkCommand":
        await OnInsertMediaLinkAsync();
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

  private void SetUndefinedBirthDate() => BirthDate = Date.Create(0, DateStatus.Unknown);

  private void SetUndefinedDeathDate() => DeathDate = Date.Create(0, DateStatus.Unknown);
}
