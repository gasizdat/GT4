using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Extensions;
using GT4.UI.Utils.Formatters;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(FamilyName), "FamilyName")]
public partial class FamilyPage : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IComparer<PersonInfo> _PersonInfoComparer;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly CreateOrUpdatePersonDialog.Factory _CreateOrUpdatePersonDialogFactory;
  private readonly DataConverterResolver _DataConverterResolver;
  private readonly FilteredObservableCollection<PersonInfo> _Persons = new();
  private bool _PersonsLoaded;
  private Name? _FamilyName = null;
  private double _PersonItemMinimalWidth;
  private ProjectInfo? _LastProjectInfo;
  private PhotoInfo[] _Photos = [];
  private AttachmentInfo[] _Attachments = [];

  public FamilyPage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    [FromKeyedServices(NameFormat.ShortPersonName)]
    IComparer<PersonInfo>? personInfoComparerByShortNames,
    IComparer<PersonInfo> personInfoComparer,
    IAlertService alertService,
    INavigationService navigationService,
    IBiologicalSexFormatter biologicalSexFormatter,
    INameTypeFormatter nameTypeFormatter,
    CreateOrUpdatePersonDialog.Factory createOrUpdatePersonDialogFactory,
    DataConverterResolver dataConverterResolver
    )
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _PersonInfoComparer = personInfoComparerByShortNames ?? personInfoComparer;
    _AlertService = alertService;
    _NavigationService = navigationService;
    _NameTypeFormatter = nameTypeFormatter;
    _CreateOrUpdatePersonDialogFactory = createOrUpdatePersonDialogFactory;
    _DataConverterResolver = dataConverterResolver;

    _Persons.Filter = (_, person) => FilterView.Matches(person);

    MemberItemTappedCommand = new SafeCommand<PersonInfo>(OnOpenPerson, _AlertService);
    PageCommand = new SafeCommand(OnPageCommand, _AlertService);

    InitializeComponent();

    FilterView.Initialize(
      biologicalSexFormatter,
      _CancellationTokenProvider,
      _CurrentProjectProvider,
      _AlertService,
      () => [.. _Persons.AllItems]);
    FilterView.Changed += (_, _) => _Persons.Update();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  public Name? FamilyName
  {
    get => _FamilyName;
    set
    {
      if (_FamilyName?.Id != value?.Id)
      {
        _FamilyName = value;
        Refresh();
      }
    }
  }

  public double PersonItemMinimalWidth => _PersonItemMinimalWidth;

  public ICommand MemberItemTappedCommand { get; init; }

  public ICommand PageCommand { get; init; }

  public NameFormat PersonNamesFormat => NameFormat.ShortPersonName;

  public ICollection<PersonInfo> Persons
  {
    get
    {
      if (FamilyName is null)
      {
        return _Persons.Items;
      }
      var familyName = FamilyName;

      async Task ListPersonsAsync(Name familyName)
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var project = _CurrentProjectProvider.Project;
        var startInfo = _CurrentProjectProvider.Info;
        PersonInfo[] persons;
        // The sentinel "no family" bucket has no Name.Id of its own, so there is no family media to fetch.
        var familyInfoTask = IsNoFamilyMode
          ? Task.FromResult(new FamilyFullInfo(familyName, null, [], []))
          : project.FamilyManager.GetFamilyFullInfoAsync(familyName, token);
        if (IsNoFamilyMode)
        {
          var allPersons = await project
            .PersonManager
            .GetPersonInfosAsync(selectMainPhoto: true, token);
          persons = [.. allPersons.Where(FamilyInfoItem.HasNoFamily)];
        }
        else
        {
          persons = await project
            .PersonManager
            .GetPersonInfosByNameAsync(name: familyName, selectMainPhoto: true, token);
        }

        persons = [.. persons.OrderBy(item => item, _PersonInfoComparer)];
        var (photos, attachments) = await LoadFamilyMediaAsync(await familyInfoTask, token);

        await SafeTask.RunOnMainThread(() =>
        {
          if (startInfo != _CurrentProjectProvider.Info)
          {
            Refresh();
            return;
          }

          _Persons.Clear();
          _Persons.AddRange(persons);
          _Photos = photos;
          _Attachments = attachments;
          FilterView.ResetFilterData();
          this.RefreshView();
        }, _AlertService);
      }

      if (!_PersonsLoaded)
      {
        _PersonsLoaded = true;
        SafeTask.Run(() => ListPersonsAsync(familyName), _AlertService);
      }

      return _Persons.Items;
    }
  }

  public PhotoInfo[] Photos => _Photos;

  public bool ShowPhotos => _Photos.Length != 0;

  public AttachmentInfo[] Attachments => _Attachments;

  public bool ShowAttachments => _Attachments.Length != 0;

  public string RemoveFamilyToolbarItemName =>
    string.Format(UIStrings.MenuItemNameRemove_1, _FamilyName?.Value ?? string.Empty);

  public string EditFamilyToolbarItemName =>
    string.Format(UIStrings.MenuItemNameEdit_1, _FamilyName?.Value ?? string.Empty);

  public bool EnableFamilyChanges => !IsNoFamilyMode;

  protected override void OnSizeAllocated(double width, double height)
  {
    const double PercentageOfWidth = 0.9;
    const int ItemsPerRow = 2;

    base.OnSizeAllocated(width, height);
    _PersonItemMinimalWidth = width * PercentageOfWidth / ItemsPerRow;

    OnPropertyChanged(nameof(PersonItemMinimalWidth));
  }

  private void Refresh()
  {
    _LastProjectInfo = _CurrentProjectProvider.Info;
    _PersonsLoaded = false;
    this.RefreshView();
  }

  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    if (_LastProjectInfo != _CurrentProjectProvider.Info)
    {
      Refresh();
    }
  }

  private bool IsNoFamilyMode => _FamilyName?.Id == FamilyInfoItem.NoFamilyName.Id;

  private async Task<(PhotoInfo[] Photos, AttachmentInfo[] Attachments)> LoadFamilyMediaAsync(FamilyFullInfo familyInfo, CancellationToken token)
  {
    PhotoInfo GetDefaultPhotoInfo() => new(ImageUtils.ImageFromRawResource("family_stub.png", null), null);

    Data[] photoData = familyInfo.MainPhoto is null
      ? [.. familyInfo.AdditionalPhotos]
      : [familyInfo.MainPhoto, .. familyInfo.AdditionalPhotos];
    var photos = (await Task.WhenAll(photoData.Select(data => _DataConverterResolver(data.Category).ToObjectAsync(data, token))))
      .Select(obj => obj is PhotoInfo photoInfo ? photoInfo : GetDefaultPhotoInfo())
      .ToArray();

    var attachments = await Task.WhenAll(familyInfo.Attachments.Select(data => AttachmentInfo.CreateAsync(data, token)));

    return (photos, attachments);
  }

  private async Task OnOpenAttachmentAsync(AttachmentInfo attachment)
  {
    using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
    await attachment.OpenAsync(token);
  }

  private async Task OnDeleteFamily()
  {
    if (_FamilyName is null)
    {
      return;
    }

    var confirmationText = string.Format(UIStrings.AlertTextRemoveFamilyConfirmationText_1, _FamilyName.Value);
    if (!await _AlertService.ShowConfirmationAsync(confirmationText))
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider
      .Project
      .FamilyManager
      .RemoveFamilyAsync(_FamilyName, token);

    await _NavigationService.GoToAsync("..", true);
  }

  private async Task OnCreatePerson()
  {
    var dialog = _CreateOrUpdatePersonDialogFactory.Create(null);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null || _FamilyName is null)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    // In no-family mode the sentinel Name must not be attached to the person -- the new person is
    // created family-less on purpose.
    var person = IsNoFamilyMode
      ? info
      : _CurrentProjectProvider
        .Project
        .FamilyManager
        .SetUpPersonFamily(info, _FamilyName);

    await _CurrentProjectProvider
      .Project
      .PersonManager
      .AddPersonAsync(person, token);

    Refresh();
  }

  protected async Task OnOpenPerson(PersonInfo familyMember) => await _NavigationService.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = familyMember });

  protected async Task OnPageCommand(object parameter)
  {
    switch (parameter)
    {
      case string commandName when commandName == "RemoveFamily":
        await OnDeleteFamily();
        break;

      case string commandName when commandName == "EditFamily":
        await CreateOrUpdateNameDialog.UpdateNameAsync(
          FamilyName!,
          _CurrentProjectProvider,
          _CancellationTokenProvider,
          _NameTypeFormatter,
          _AlertService,
          Navigation,
          _DataConverterResolver);
        Refresh();
        break;

      case string commandName when commandName == "CreatePerson":
        await OnCreatePerson();
        break;

      case string commandName when commandName == "Refresh":
        Refresh();
        break;

      case AttachmentInfo attachment:
        await OnOpenAttachmentAsync(attachment);
        break;
    }
  }
}
