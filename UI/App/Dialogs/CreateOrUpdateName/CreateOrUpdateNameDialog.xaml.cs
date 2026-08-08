using GT4.Core.Project.Abstraction;
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

public partial class CreateOrUpdateNameDialog : ContentPage
{
  public record FamilyInfo(string Name, string MaleName, string FemaleName, Data[] Photos, Data[] Attachments);

  private readonly NameType _NameType;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterFactory;
  private readonly TaskCompletionSource<FamilyInfo?> _Info = new(null);
  private readonly string _DialogButtonName;
  private readonly ICommand _DialogCommand;
  private readonly ICommand _MediaCommand;
  private readonly ObservableCollection<PersonDataItem> _Photos = new();
  private readonly ObservableCollection<PersonDataItem> _Attachments = new();
  private string _GeneralName = string.Empty;
  private string _MaleName = string.Empty;
  private string _FemaleName = string.Empty;
  private bool _MaleNameEnteredManually = false;
  private bool _FemaleNameEnteredManually = false;
  private bool _IsModified = false;
  private bool _FocusGenericName = false;
  private bool _FocusMaleName = false;
  private bool _FocusFemaleName = false;

  protected bool NotReady =>
    string.IsNullOrWhiteSpace(_GeneralName) ||
    !_IsModified ||
    ShowDeclensionNames && (string.IsNullOrWhiteSpace(_MaleName) || string.IsNullOrWhiteSpace(_FemaleName));

  protected bool IsModified
  {
    set
    {
      _IsModified = value;
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public CreateOrUpdateNameDialog(
    NameType nameType,
    INameTypeFormatter nameTypeFormatter,
    IAlertService alertService,
    ICancellationTokenProvider cancellationTokenProvider,
    DataConverterResolver dataConverterFactory)
  {
    _AlertService = alertService;
    _CancellationTokenProvider = cancellationTokenProvider;
    _DataConverterFactory = dataConverterFactory;
    var nameTypeName = nameTypeFormatter.ToString(nameType);
    _DialogButtonName = string.Format(UIStrings.BtnNameCreateName_1, nameTypeName);
    _DialogCommand = new SafeCommand(OnCreateFamily, alertService);
    _MediaCommand = new SafeCommand(OnMediaCommand, alertService);

    switch (nameType)
    {
      case NameType.FamilyName:
      case NameType.FirstName | NameType.MaleDeclension:
      case NameType.FirstName | NameType.FemaleDeclension:
      // Imported patronymics/last names can be parentless (orphans); editing one in isolation is a
      // single-value edit (no declension fields), so the dialog accepts these standalone leaf types.
      case NameType.Patronymic | NameType.MaleDeclension:
      case NameType.Patronymic | NameType.FemaleDeclension:
      case NameType.LastName | NameType.MaleDeclension:
      case NameType.LastName | NameType.FemaleDeclension:
        _NameType = nameType;
        break;

      default:
        throw new NotSupportedException($"Unsupported name type {nameType}");
    }

    InitializeComponent();

    IsModified = false;
  }

  public CreateOrUpdateNameDialog(
    Name name,
    Name? maleName,
    Name? femaleName,
    INameTypeFormatter nameTypeFormatter,
    IAlertService alertService,
    ICancellationTokenProvider cancellationTokenProvider,
    DataConverterResolver dataConverterFactory,
    Data[]? familyData = null)
    : this(name.Type, nameTypeFormatter, alertService, cancellationTokenProvider, dataConverterFactory)
  {
    var nameTypeName = nameTypeFormatter.ToString(name.Type);
    _DialogButtonName = string.Format(UIStrings.BtnNameUpdateName_1, nameTypeName);
    GeneralName = name.Value;
    MaleName = maleName?.Value ?? string.Empty;
    FemaleName = femaleName?.Value ?? string.Empty;

    foreach (var data in familyData ?? [])
    {
      if (data.Category.IsPhoto())
      {
        _Photos.Add(GetFamilyData(data, data.Category));
      }
      else if (data.Category.IsAttachment())
      {
        _Attachments.Add(GetFamilyData(data, data.Category));
      }
    }

    IsModified = false;

    OnPropertyChanged(nameof(DialogTitle));
  }

  public bool ShowDeclensionNames =>
    _NameType == NameType.FamilyName || _NameType == (NameType.FirstName | NameType.MaleDeclension);

  public bool ShowFamilyMedia => _NameType == NameType.FamilyName;

  public ICollection<PersonDataItem> Photos => _Photos;

  public ICollection<PersonDataItem> Attachments => _Attachments;

  public ICommand MediaCommand => _MediaCommand;

  public string GeneralName
  {
    get => _GeneralName;
    set
    {
      if (_GeneralName == value)
      {
        return;
      }

      _GeneralName = value;
      if (!_MaleNameEnteredManually)
      {
        var maleName = NameDeclension.ToMaleDeclension(Language.Current, _NameType, value);
        MaleName = maleName;
        _MaleNameEnteredManually = false;
      }

      if (!_FemaleNameEnteredManually)
      {
        var femaleName = NameDeclension.ToFemaleDeclension(Language.Current, _NameType, value);
        FemaleName = femaleName;
        _FemaleNameEnteredManually = false;
      }

      IsModified = true;
      OnPropertyChanged(nameof(GeneralName));
    }
  }

  public string MaleName
  {
    get => _MaleName;
    set
    {
      if (_MaleName == value)
      {
        return;
      }

      _MaleName = value;
      _MaleNameEnteredManually = !string.IsNullOrEmpty(value);

      IsModified = true;
      OnPropertyChanged(nameof(MaleName));
    }
  }

  public string FemaleName
  {
    get => _FemaleName;
    set
    {
      _FemaleName = value;
      _FemaleNameEnteredManually = !string.IsNullOrEmpty(value);
      IsModified = true;
      OnPropertyChanged(nameof(FemaleName));
    }
  }

  public string GeneralNameDescription => _NameType switch
  {
    NameType.FamilyName => UIStrings.FieldFamilyNameEntry,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.FieldFirstNameMaleEntry,
    NameType.FirstName | NameType.FemaleDeclension => UIStrings.FieldFirstNameFemaleEntry,
    NameType.Patronymic | NameType.MaleDeclension => UIStrings.FieldPatronymicMaleEntry,
    NameType.Patronymic | NameType.FemaleDeclension => UIStrings.FieldPatronymicFemaleEntry,
    NameType.LastName | NameType.MaleDeclension => UIStrings.FieldLastNameMaleEntry,
    NameType.LastName | NameType.FemaleDeclension => UIStrings.FieldLastNameFemaleEntry,
    _ => throw new ApplicationException(nameof(GeneralNameDescription))
  };

  public string GeneralNamePlaceholder => _NameType switch
  {
    NameType.FamilyName => UIStrings.TxtPlaceholderFamilyName,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.TxtPlaceholderMaleName,
    NameType.FirstName | NameType.FemaleDeclension => UIStrings.TxtPlaceholderFemaleName,
    NameType.Patronymic | NameType.MaleDeclension => UIStrings.TxtPlaceholderPatronymicMale,
    NameType.Patronymic | NameType.FemaleDeclension => UIStrings.TxtPlaceholderPatronymicFemale,
    NameType.LastName | NameType.MaleDeclension => UIStrings.TxtPlaceholderLastNameMale,
    NameType.LastName | NameType.FemaleDeclension => UIStrings.TxtPlaceholderLastNameFemale,
    _ => throw new ApplicationException(nameof(GeneralNamePlaceholder))
  };

  // The declension labels below are only shown when ShowDeclensionNames is true (FamilyName and
  // FirstName|Male); every other supported type — including the standalone orphan leaves — hides
  // these fields, so their labels are empty.
  public string MaleNameDescription => _NameType switch
  {
    NameType.FamilyName => UIStrings.FieldLastNameMaleEntry,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.FieldPatronymicMaleEntry,
    _ => string.Empty
  };

  public string MaleNamePlaceholder => _NameType switch
  {
    NameType.FamilyName => UIStrings.TxtPlaceholderLastNameMale,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.TxtPlaceholderPatronymicMale,
    _ => string.Empty
  };

  public string FemaleNameDescription => _NameType switch
  {
    NameType.FamilyName => UIStrings.FieldLastNameFemaleEntry,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.FieldPatronymicFemaleEntry,
    _ => string.Empty
  };

  public string FemaleNamePlaceholder => _NameType switch
  {
    NameType.FamilyName => UIStrings.TxtPlaceholderLastNameFemale,
    NameType.FirstName | NameType.MaleDeclension => UIStrings.TxtPlaceholderPatronymicFemale,
    _ => string.Empty
  };

  public Task<FamilyInfo?> Info => _Info.Task;

  public string DialogButtonName => NotReady ? UIStrings.BtnNameCancel : _DialogButtonName;

  public string DialogTitle => _DialogButtonName;

  public bool FocusGenericName
  {
    get => _FocusGenericName;
    set
    {
      if (_FocusGenericName != value)
      {
        _FocusGenericName = value;
        OnPropertyChanged(nameof(FocusGenericName));
      }
    }
  }

  public bool FocusMaleName
  {
    get => _FocusMaleName;
    set
    {
      if (_FocusMaleName != value)
      {
        _FocusMaleName = value;
        OnPropertyChanged(nameof(FocusMaleName));
      }
    }
  }

  public bool FocusFemaleName
  {
    get => _FocusFemaleName;
    set
    {
      if (_FocusFemaleName != value)
      {
        _FocusFemaleName = value;
        OnPropertyChanged(nameof(FocusFemaleName));
      }
    }
  }

  public ICommand DialogCommand => _DialogCommand;

  private void OnCreateFamily()
  {
    if (NotReady)
    {
      _Info.SetResult(null);
      return;
    }

    var photos = _Photos.Select(item => item.Info).ToArray();
    var mainPhoto = photos.FirstOrDefault();
    var additionalPhotos = photos.Skip(1).Select(photo => photo with { Category = photo.Category.AsAdditionalPhoto() });
    // Position in _Photos (index 0 = main), not the category assigned at add time, is authoritative --
    // MoveFamilyPhotoToLeft/Right reorders the collection without touching each item's DataCategory.
    Data[] allPhotos = mainPhoto is null
      ? [.. additionalPhotos]
      : [mainPhoto with { Category = mainPhoto.Category.AsMainPhoto() }, .. additionalPhotos];

    var name = GeneralName;
    var maleName = ShowDeclensionNames ? MaleName : string.Empty;
    var femaleName = ShowDeclensionNames ? FemaleName : string.Empty;
    var attachments = _Attachments.Select(item => item.Info).ToArray();
    _Info.SetResult(new(name, maleName, femaleName, allPhotos, attachments));
  }

  private PersonDataItem GetFamilyData(Data data, DataCategory dataCategory) =>
    new(data, _DataConverterFactory(dataCategory), _CancellationTokenProvider, _AlertService);

  private static byte[] FromStream(Stream stream)
  {
    using var ret = new MemoryStream();
    stream.CopyTo(ret);
    return ret.ToArray();
  }

  private static async Task<IEnumerable<FileResult>?> PickFilesAsync(PickOptions pickOptions, bool allowMultiple)
  {
    if (allowMultiple)
    {
      var files = await FilePicker.Default.PickMultipleAsync(pickOptions);
      return files?.Where(f => f is not null).Select(r => r!);
    }

    var result = await FilePicker.Default.PickAsync(pickOptions);
    return result is null ? null : [result];
  }

  private async Task OnAddOrUpdateFamilyPhotoAsync(PersonDataItem? photo)
  {
    var pickOptions = new PickOptions
    {
      PickerTitle = UIStrings.FileDialogSelectPictures,
      FileTypes = FilePickerFileType.Images
    };
    var results = await PickFilesAsync(pickOptions, allowMultiple: photo is null);
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
        var category = _Photos.Count() == 0 ? DataCategory.FamilyMainPhoto : DataCategory.FamilyPhoto;
        var item = GetFamilyData(photoAsset with { Category = category }, category);
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

  private async Task OnAddOrUpdateFamilyAttachmentAsync(PersonDataItem? attachment)
  {
    var pickOptions = new PickOptions { PickerTitle = UIStrings.FileDialogSelectAttachment };
    var results = await PickFilesAsync(pickOptions, allowMultiple: attachment is null);
    if (results is null)
    {
      return;
    }

    var converter = _DataConverterFactory(DataCategory.FamilyAttachment);
    IEnumerable<Stream>? streams = null;
    try
    {
      var filesContent = results.Select(file => (Stream: file.OpenReadAsync(), file.FileName, MimeType: file.ContentType)).ToArray();
      streams = await Task.WhenAll(filesContent.Select(file => file.Stream));

      using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
      foreach (var content in filesContent)
      {
        var pick = new AttachmentPick(FromStream(content.Stream.Result), content.FileName, content.MimeType);
        var attachmentAsset = await converter.FromObjectAsync(pick, token);
        if (attachmentAsset is null)
        {
          continue;
        }

        var item = GetFamilyData(attachmentAsset, DataCategory.FamilyAttachment);
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

  private void MoveItem<T>(ObservableCollection<T> collection, T item, int dIndex)
  {
    var oldIndex = collection.IndexOf(item);
    var newIndex = oldIndex + dIndex;

    if (newIndex < 0 || newIndex >= collection.Count)
    {
      throw new ApplicationException(UIStrings.ErrorTheBoundIsReached);
    }

    collection.Move(oldIndex, newIndex);
    IsModified = true;
  }

  private async Task OnMediaCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "AddFamilyPhotoCommand":
        await OnAddOrUpdateFamilyPhotoAsync(null);
        break;
      case string commandName when commandName == "AddFamilyAttachmentCommand":
        await OnAddOrUpdateFamilyAttachmentAsync(null);
        break;

      case AdornerCommandParameter adorner when adorner.CommandName == "EditFamilyPhotoCommand" && adorner.Element is PersonDataItem photo:
        await OnAddOrUpdateFamilyPhotoAsync(photo);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveFamilyPhotoCommand" && adorner.Element is PersonDataItem photo:
        _Photos.Remove(photo);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveFamilyPhotoToLeftCommand" && adorner.Element is PersonDataItem photo:
        MoveItem(_Photos, photo, -1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveFamilyPhotoToRightCommand" && adorner.Element is PersonDataItem photo:
        MoveItem(_Photos, photo, 1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "EditFamilyAttachmentCommand" && adorner.Element is PersonDataItem attachment:
        await OnAddOrUpdateFamilyAttachmentAsync(attachment);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "RemoveFamilyAttachmentCommand" && adorner.Element is PersonDataItem attachment:
        _Attachments.Remove(attachment);
        IsModified = true;
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveFamilyAttachmentUpCommand" && adorner.Element is PersonDataItem attachment:
        MoveItem(_Attachments, attachment, -1);
        break;
      case AdornerCommandParameter adorner when adorner.CommandName == "MoveFamilyAttachmentDownCommand" && adorner.Element is PersonDataItem attachment:
        MoveItem(_Attachments, attachment, 1);
        break;
    }
  }

  private record NamesGroup(Name FirstName, Name? MaleName, Name? FemaleName);

  public static async Task UpdateNameAsync(
    Name name,
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    INameTypeFormatter nameTypeFormatter,
    IAlertService alertService,
    INavigation navigation,
    DataConverterResolver dataConverterFactory)
  {
    async Task<NamesGroup> GetNameWithSubnames()
    {
      using var token = cancellationTokenProvider.CreateDbCancellationToken();
      var names = await currentProjectProvider
        .Project
        .Names
        .TryGetNameWithSubnamesByIdAsync(name.Id, token);

      var firstName = names?.Single(n => n.Type.HasFlag(NameType.FirstName) || n.Type.HasFlag(NameType.FamilyName))
        ?? throw new Exception($"A parent name was not found for '{name.Value}'");
      var maleName = names?.SingleOrDefault(
        n => (n.Type.HasFlag(NameType.Patronymic) || n.Type.HasFlag(NameType.LastName)) && n.Type.HasFlag(NameType.MaleDeclension));
      var femaleName = names?.SingleOrDefault(
        n => (n.Type.HasFlag(NameType.Patronymic) || n.Type.HasFlag(NameType.LastName)) && n.Type.HasFlag(NameType.FemaleDeclension));
      var ret = new NamesGroup(firstName, maleName, femaleName);

      return ret;
    }

    // Imported patronymics/last names can have no parent first name (orphans). They have no
    // declensions to gather, so edit the single value directly instead of walking to a parent that
    // GetNameWithSubnames would fail to find.
    var isOrphanDeclension = name.ParentId is null &&
      (name.Type.HasFlag(NameType.Patronymic) || name.Type.HasFlag(NameType.LastName));
    var names = isOrphanDeclension
      ? new NamesGroup(name, null, null)
      : name.Type switch
      {
        NameType.FirstName | NameType.MaleDeclension or
        NameType.Patronymic | NameType.MaleDeclension or
        NameType.Patronymic | NameType.FemaleDeclension or
        NameType.LastName | NameType.MaleDeclension or
        NameType.LastName | NameType.FemaleDeclension or
        NameType.FamilyName => await GetNameWithSubnames(),
        _ => new NamesGroup(name, null, null),
      };
    var focusGenericName = isOrphanDeclension ||
      name.Type.HasFlag(NameType.FirstName) || name.Type.HasFlag(NameType.FamilyName);

    Data[] familyData = [];
    if (name.Type == NameType.FamilyName)
    {
      using var familyDataToken = cancellationTokenProvider.CreateDbCancellationToken();
      familyData = await currentProjectProvider.Project.FamilyManager.GetFamilyDataAsync(names.FirstName, familyDataToken);
    }

    var dialog = new CreateOrUpdateNameDialog(
      names.FirstName, names.MaleName, names.FemaleName, nameTypeFormatter, alertService, cancellationTokenProvider, dataConverterFactory, familyData)
    {
      FocusGenericName = focusGenericName,
      FocusMaleName = !focusGenericName && name.Type.HasFlag(NameType.MaleDeclension),
      FocusFemaleName = !focusGenericName && name.Type.HasFlag(NameType.FemaleDeclension)
    };

    await navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await navigation.PopModalAsync();

    if (info is null)
      return;

    using var token = cancellationTokenProvider.CreateDbCancellationToken();
    using var transaction = await currentProjectProvider.Project.BeginTransactionAsync(token);
    var tasks = new List<Task>
    {
      currentProjectProvider
        .Project
        .Names
        .UpdateName(names.FirstName with { Value = info.Name }, token)
    };
    if (names.MaleName != null)
    {
      tasks.Add(currentProjectProvider
          .Project
          .Names
          .UpdateName(names.MaleName with { Value = info.MaleName }, token));
    }
    else if (!string.IsNullOrEmpty(info.MaleName))
    {
      var nameType = names.FirstName.Type switch
      {
        NameType.FamilyName => NameType.LastName,
        NameType.FirstName => NameType.Patronymic,
        _ => throw new ApplicationException(nameof(UpdateNameAsync))
      } | NameType.MaleDeclension;
      tasks.Add(currentProjectProvider
          .Project
          .Names
          .AddNameAsync(info.MaleName, nameType, names.FirstName, token));
    }
    if (names.FemaleName != null)
    {
      tasks.Add(currentProjectProvider
          .Project
          .Names
          .UpdateName(names.FemaleName with { Value = info.FemaleName }, token));
    }
    else if (!string.IsNullOrEmpty(info.FemaleName))
    {
      var nameType = names.FirstName.Type switch
      {
        NameType.FamilyName => NameType.LastName,
        NameType.FirstName => NameType.Patronymic,
        _ => throw new ApplicationException(nameof(UpdateNameAsync))
      } | NameType.FemaleDeclension;
      tasks.Add(currentProjectProvider
          .Project
          .Names
          .AddNameAsync(info.FemaleName, nameType, names.FirstName, token));
    }
    if (name.Type == NameType.FamilyName)
    {
      Data[] familyDataSet = [.. info.Photos, .. info.Attachments];
      tasks.Add(currentProjectProvider
          .Project
          .FamilyManager
          .UpdateFamilyDataAsync(names.FirstName, familyDataSet, token));
    }
    await Task.WhenAll(tasks);
    await transaction.CommitAsync(token);
  }
}
