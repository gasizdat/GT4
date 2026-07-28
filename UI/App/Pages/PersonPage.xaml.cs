using GT4.Core.Gedcom;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Converters;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Utils.Formatters;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Extensions;
using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class PersonPage : ContentPage
{
  // Spelled out rather than taken from GedcomTags, which is internal to Core.Gedcom -- the same way
  // GedcomDataConverter names the tags it renders specially.
  private const string FamilyTag = "FAM";
  private const string MediaTag = "OBJE";

  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly IDataConverter _TextConverter;
  private readonly IDataConverter _GedcomConverter;
  private readonly OptionalDataConverterResolver _DataConverterResolver;
  private readonly ICommand _PageCommand;
  private readonly RelativeTree _Relatives;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  private readonly CreateOrUpdatePersonDialog.Factory _CreateOrUpdatePersonDialogFactory;
  private ObservableCollection<PersonInfo> _NavigationHistory = new();
  private int _NavigationIndex = -1;
  private PersonFullInfo _PersonFullInfo = PersonFullInfo.Empty;
  private ImageSource[] _Photos = [];
  private string?[] _Captions = [];
  private IReadOnlyDictionary<int, string> _MediaSources = ReadOnlyDictionary<int, string>.Empty;
  private AttachmentInfo[] _Attachments = [];
  private string _Biography = string.Empty;
  private PersonPageSmartLayout _SmartLayout = new();
  private bool _ExpandAll = false;
  private RelativeInfo[] _AllRoots = [];
  private ProjectInfo? _LastProjectInfo;

  public PersonPage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IDateSpanFormatter dateSpanFormatter,
    IDateFormatter dateFormatter,
    INameFormatter nameFormatter,
    [FromKeyedServices(DataCategory.PersonBio)]
    IDataConverter textConverter,
    [FromKeyedServices(DataCategory.PersonGedcomTags)]
    IDataConverter gedcomConverter,
    OptionalDataConverterResolver dataConverterResolver,
    IAlertService alertService,
    INavigationService navigationService,
    IBiologicalSexFormatter biologicalSexFormatter,
    CreateOrUpdatePersonDialog.Factory createOrUpdatePersonDialogFactory
    )
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _DateSpanFormatter = dateSpanFormatter;
    _DateFormatter = dateFormatter;
    _NameFormatter = nameFormatter;
    _TextConverter = textConverter;
    _GedcomConverter = gedcomConverter;
    _DataConverterResolver = dataConverterResolver;
    _AlertService = alertService;
    _NavigationService = navigationService;
    _CreateOrUpdatePersonDialogFactory = createOrUpdatePersonDialogFactory;
    _PageCommand = new SafeCommand(OnPageCommand, _AlertService);
    _Relatives = new RelativeTree(_CurrentProjectProvider, _CancellationTokenProvider, _AlertService);

    InitializeComponent();

    FilterView.Initialize(
      biologicalSexFormatter,
      _CancellationTokenProvider,
      _CurrentProjectProvider,
      _AlertService,
      () => _AllRoots);
    FilterView.Changed += (_, _) => RefreshRelatives();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  protected ScrollView BodyScroll => BodyScrollView;

  protected ImagePresenter PersonPhotoView => PersonPhoto;

  protected CollectionView RelativesListView => RelativesView;

  public void ShowPersonInfo(Person person, bool addToNavigation)
  {
    ExpandAll = false;
    Task.Run(async () => await GetPersonDataAsync(person, addToNavigation));
  }

  public bool ExpandAll
  {
    get => _ExpandAll;
    set
    {
      _ExpandAll = value;
      OnPropertyChanged(nameof(ExpandAll));
      OnPropertyChanged(nameof(ToggleAllButtonName));
      OnPropertyChanged(nameof(ToggleAllMenuItemName));
    }
  }

  public string ToggleAllButtonName => ExpandAll ? "⏫" : "⏬";

  public string ToggleAllMenuItemName =>
    string.Format(ExpandAll ? UIStrings.MenuItemCollapseAll_1 : UIStrings.MenuItemExpandAll_1, ToggleAllButtonName);

  private void RefreshRelatives() => _Relatives.SetFilter(FilterView.IsAnyFilterActive, r => FilterView.Matches(r));

  public ICommand PageCommand => _PageCommand;

  public string EditPersonToolbarItemName => string.Format(UIStrings.MenuItemNameEdit_1, ShortName);

  public string RemovePersonToolbarItemName => string.Format(UIStrings.MenuItemNameRemove_1, ShortName);

  public string ShortName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.ShortPersonName);

  public string FullName => _NameFormatter.ToString(_PersonFullInfo, NameFormat.FullPersonName);

  public string BirthDate => _DateFormatter.ToString(_PersonFullInfo.BirthDate);

  public string DeathDate => _DateFormatter.ToString(_PersonFullInfo.DeathDate);

  public bool ShowDeathDate => _PersonFullInfo.DeathDate.HasValue;

  public string Age
  {
    get
    {
      var age = _PersonFullInfo.DeathDate.GetValueOrDefault(Date.Now) - _PersonFullInfo.BirthDate;
      return _DateSpanFormatter.ToString(age);
    }
  }

  public bool ShowSinceBirth
  {
    get
    {
      return _PersonFullInfo.BirthDate.Status < DateStatus.Unknown && _PersonFullInfo.DeathDate.HasValue;
    }
  }

  public string SinceBirth
  {
    get
    {
      var dateSpan = Date.Now - _PersonFullInfo.BirthDate;
      var ret = _DateSpanFormatter.ToString(dateSpan with { Status = DateStatus.DayUnknown });
      ret = string.Format(UIStrings.PersonSinceBirthDay_1, ret);

      return ret;
    }
  }

  public ICollection Relatives => _Relatives.Rows;

  public ImageSource[] Photos => _Photos;

  public string?[] Captions => _Captions;

  public IReadOnlyDictionary<int, string> MediaSources => _MediaSources;

  public AttachmentInfo[] Attachments => _Attachments;

  public bool ShowAttachments => _Attachments.Length != 0;

  public PersonFullInfo PersonFullInfo => _PersonFullInfo;

  public PersonInfo PersonInfo
  {
    set => ShowPersonInfo(value, true);
  }

  public PersonPageSmartLayout SmartLayout => _SmartLayout;

  public string Biography => _Biography;

  public bool ShowBiography => !string.IsNullOrWhiteSpace(_Biography);

  // The biography block doubles as the home for the read-only GEDCOM details: the stored bio first, then
  // the person's own residual tags, then their couples', so a person carrying only imported GEDCOM data
  // still shows the block.
  private static string CombineBiography(params string?[] sections)
  {
    var present = sections.Where(section => !string.IsNullOrWhiteSpace(section));
    return string.Join("\n\n", present);
  }

  public Name FamilyName =>
    _PersonFullInfo.Names.SingleOrDefault(n => n.Type == NameType.FamilyName, FamilyInfoItem.NoFamilyName);

  public string GoToFamilyName => string.Format(UIStrings.MenuItemGotoFamily_1, FamilyName.Value);

  public ICollection NavigationHistory => _NavigationHistory;

  public PersonInfo? CurrentPerson
  {
    get => _NavigationIndex >= 0 ? _NavigationHistory[_NavigationIndex] : null;
    set
    {
      if (value is null)
      {
        return;
      }

      var index = _NavigationHistory.IndexOf(value);
      if (index != _NavigationIndex)
      {
        _NavigationIndex = index;
        OnPropertyChanged(nameof(CurrentPerson));
        ShowPersonInfo(value, false);
      }
    }
  }

  protected override void OnSizeAllocated(double width, double height)
  {
    base.OnSizeAllocated(width, height);
    var widthInPixels = () => width * DeviceDisplay.Current.MainDisplayInfo.Density;
    if (width < height || widthInPixels() < 900)
    {
      _SmartLayout = new PersonPageSmartLayout(
        Image: new GridLayout(Column: 0, ColumnSpan: 2, Row: 0, RowSpan: 1),
        Relatives: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1),
        Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 2, RowSpan: 1));
    }
    else
    {
      _SmartLayout = new PersonPageSmartLayout(
        Image: new GridLayout(Column: 0, ColumnSpan: 1, Row: 0, RowSpan: 1),
        Relatives: new GridLayout(Column: 1, ColumnSpan: 1, Row: 0, RowSpan: 1),
        Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1));
    }
    OnPropertyChanged(nameof(SmartLayout));
    UpdatePersonPhotoStickyPosition();
  }

  // In the wide (landscape) layout the photo and the relatives list share the same auto-sized grid
  // row, which grows to fit however many relatives there are. Left alone, the photo scrolls away with
  // the rest of that row as soon as the relatives list is taller than the viewport. Instead, pin the
  // photo to the top of the viewport by counter-translating it with the scroll offset, but only up to
  // the point where its bottom edge reaches the bottom of that shared row -- beyond that it scrolls
  // away normally with the rest of the row, so it never escapes its own grid cell.
  private void UpdatePersonPhotoStickyPosition(double scrollY = -1)
  {
    if (scrollY < 0)
    {
      scrollY = BodyScrollView.ScrollY;
    }

    if (_SmartLayout.Image.Row != _SmartLayout.Relatives.Row || PersonPhoto.Height <= 0 || RelativesView.Height <= 0)
    {
      PersonPhoto.TranslationY = 0;
      return;
    }

    var maxTranslation = Math.Max(0, RelativesView.Height - PersonPhoto.Height);
    PersonPhoto.TranslationY = Math.Clamp(scrollY, 0, maxTranslation);
  }

  // See ProjectPage.OnNavigatedTo: returning from a subpage (family, tree, another person) that
  // committed an edit must re-fetch the current person's data.
  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    if (_LastProjectInfo != _CurrentProjectProvider.Info)
    {
      Refresh();
    }
  }

  private void Refresh()
  {
    _LastProjectInfo = _CurrentProjectProvider.Info;
    ShowPersonInfo(_PersonFullInfo, false);
  }

  private void OnBodyScrolled(object? sender, ScrolledEventArgs e) => UpdatePersonPhotoStickyPosition(e.ScrollY);

  private void OnPersonPhotoOrRelativesSizeChanged(object? sender, EventArgs e) => UpdatePersonPhotoStickyPosition();

  private void OnPersonLinkTapped(object? sender, int personId) =>
    SafeTask.Run(() => NavigateToPersonLinkAsync(personId), _AlertService);

  // A dangling link (the referenced person was since deleted) is simply inert.
  protected async Task NavigateToPersonLinkAsync(int personId)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var person = await _CurrentProjectProvider.Project.Persons.TryGetPersonByIdAsync(personId, token);
    if (person is not null)
    {
      ShowPersonInfo(person, true);
    }
  }

  private void OnAttachmentLinkTapped(object? sender, int attachmentId) =>
    SafeTask.Run(() => OpenAttachmentLinkAsync(attachmentId), _AlertService);

  // A dangling link (the referenced attachment was since removed) is simply inert.
  protected async Task OpenAttachmentLinkAsync(int attachmentId)
  {
    var attachment = _Attachments.FirstOrDefault(a => a.Data.Id == attachmentId);
    if (attachment is not null)
    {
      await OnOpenAttachmentAsync(attachment);
    }
  }

  // attachment.FileName is often a full original path -- a common artifact of GEDCOM imports.
  private async Task OnOpenAttachmentAsync(AttachmentInfo attachment)
  {
    using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
    var fileName = FileNameUtils.Sanitize(attachment.FileName, "attachment");
    var path = Path.Combine(FileSystem.CacheDirectory, fileName);
    var bytes = GedcomPhotoResidue.ExtractImageBytes(attachment.Data.Content);
    await File.WriteAllBytesAsync(path, bytes, token);

    await Launcher.Default.OpenAsync(new OpenFileRequest(fileName, new ReadOnlyFile(path)));
  }

  private async Task OnGotoFamilyAsync() =>
    await _NavigationService.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { ["FamilyName"] = FamilyName });

  private static async Task<AttachmentInfo> ToAttachmentInfoAsync(Data data, CancellationToken token)
  {
    var fileName = await GedcomPhotoResidue.ExtractFileNameAsync(data, token);
    var title = await GedcomPhotoResidue.ExtractTitleAsync(data, token);

    return new AttachmentInfo(fileName ?? string.Empty, title, data);
  }

  /// <summary>
  /// A couple's preserved GEDCOM tags are keyed by the pair rather than carried on either person, so they
  /// are gathered from the spouse relationships instead of arriving on <see cref="PersonFullInfo"/> the way
  /// the person's own residue does. Several marriages to one spouse are one couple and one block. The media
  /// their family record carried joins that block as a link, so the documents about the marriage are reached
  /// from the narrative about it and not only from the undifferentiated attachments list.
  /// </summary>
  private async Task<string?> ReadFamilyDetailsAsync(
    IProjectDocument project,
    PersonFullInfo person,
    AttachmentInfo[] attachments,
    CancellationToken token)
  {
    var spouses = person.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse);
    var couples = spouses.GroupBy(spouse => spouse.Id).ToArray();
    Person[] spousePersons = [.. couples.Select(couple => couple.First())];
    var spouseAttachments = await project.PersonData.GetPersonDataSetAsync(spousePersons, DataCategory.PersonAttachment, token);
    var facts = new List<GedcomFact>();

    foreach (var couple in couples)
    {
      var spouse = couple.First();
      var name = _NameFormatter.ToString(spouse, NameFormat.ShortPersonName);
      var dates = couple.Select(marriage => marriage.Date);
      var residue = await GedcomFamilyResidue.ReadAsync(project, person.Id, couple.Key, name, dates, token);
      var theirs = spouseAttachments.GetValueOrDefault(couple.Key, []);
      var media = await GedcomFamilyMedia.SelectSharedAsync(person.Attachments, theirs, token);
      GedcomFact[] children = [.. residue?.Children ?? [], .. media.Select(data => ToMediaFact(data, attachments))];
      if (children.Length == 0)
      {
        continue;
      }

      facts.Add(new GedcomFact(FamilyTag, $"[{name}](person:{couple.Key})", children));
    }

    return GedcomDataConverter.RenderFamilies([.. facts]);
  }

  private static GedcomFact ToMediaFact(Data media, AttachmentInfo[] attachments)
  {
    var info = attachments.First(attachment => attachment.Data.Id == media.Id);
    return new GedcomFact(MediaTag, $"[{info.DisplayName}](attachment:{media.Id})", []);
  }

  private async Task GetPersonDataAsync(Person person, bool addToNavigation)
  {
    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var project = _CurrentProjectProvider.Project;
      var startInfo = _CurrentProjectProvider.Info;
      var personFullInfo = await project.PersonManager.GetPersonFullInfoAsync(person, token);
      var parentsTasks = project.RelativesProvider.GetParentsAsync(personFullInfo.RelativeInfos, token);
      var stepChildrenTasks = project.RelativesProvider.GetStepChildrenAsync(personFullInfo.RelativeInfos, token);
      var bioTask = _TextConverter.ToObjectAsync(personFullInfo.Biography, token);
      var gedcomTask = _GedcomConverter.ToObjectAsync(personFullInfo.GedcomData, token);
      var attachmentsTask = Task.WhenAll(
        personFullInfo.Attachments.Select(data => ToAttachmentInfoAsync(data, token)));
      await Task.WhenAll(parentsTasks, stepChildrenTasks, bioTask, gedcomTask, attachmentsTask);

      // Sequenced after the attachments: a couple's media renders under the name the attachment row carries.
      var attachments = attachmentsTask.Result;
      var familyDetails = await ReadFamilyDetailsAsync(project, personFullInfo, attachments, token);

      var parents = parentsTasks.Result;
      var stepChildren = stepChildrenTasks.Result;
      var relativesProvider = project.RelativesProvider;
      var siblings = relativesProvider.GetSiblings(personFullInfo, parents);
      var roots = AssembleRoots(personFullInfo, parents, siblings, stepChildren, relativesProvider);

      ImageSource[] photos;
      string?[] captions;
      Data[] photoData;

      if (personFullInfo.MainPhoto is null)
      {
        if (personFullInfo.AdditionalPhotos.Length != 0)
        {
          throw new ApplicationException("Person photos inconsistency");
        }

        using var readResourceToken = _CancellationTokenProvider.CreateShortOperationCancellationToken();
        var defaultImageResourceName = ImageUtils.DefaultPhotoResourceName(personFullInfo.BiologicalSex);
        var defaultPhoto = await ImageUtils.ToBytesAsync(defaultImageResourceName, readResourceToken) ?? [];
        photos = [ImageUtils.ImageFromBytes(defaultPhoto)];
        captions = [null];
        photoData = [];
      }
      else
      {
        photoData = [personFullInfo.MainPhoto, .. personFullInfo.AdditionalPhotos];
        var defaultImageResourceName = ImageUtils.DefaultPhotoResourceName(personFullInfo.BiologicalSex);
        var fallback = ImageUtils.ImageFromRawResource(defaultImageResourceName);
        var photosTask = Task.WhenAll(photoData.Select(data =>
          ImageUtils.ResolvePhotoAsync(_DataConverterResolver, data, fallback, token)));
        var captionsTask = Task.WhenAll(photoData.Select(data => GedcomPhotoResidue.ExtractTitleAsync(data, token)));
        await Task.WhenAll(photosTask, captionsTask);
        photos = photosTask.Result;
        captions = captionsTask.Result;
      }

      var mediaSources = MediaSourceUtils.BuildMediaSources([.. photoData, .. personFullInfo.Attachments]);

      // UpdateUI touches the project document again on the UI thread; SafeTask.RunOnMainThread keeps
      // an escaped exception (e.g. the project closed while backgrounding) from going unobserved.
      _ = SafeTask.RunOnMainThread(() =>
      {
        if (startInfo != _CurrentProjectProvider.Info)
        {
          Refresh();
          return;
        }

        UpdateUI(personFullInfo,
                 roots,
                 photos, captions, mediaSources, attachments, bioTask.Result as string,
                 gedcomTask.Result as string,
                 familyDetails,
                 addToNavigation);
      }, _AlertService);
    }
    catch (Exception ex) when (SafeTask.IsProjectTeardown(ex))
    {
      // The project was closed underneath us (e.g. the app is backgrounding). Nothing to surface.
      System.Diagnostics.Debug.WriteLine(ex);
    }
    catch (Exception ex)
    {
      // GetPersonDataAsync runs on a background thread (Task.Run); both the alert and the
      // navigation touch native views, so marshal them onto the UI thread.
      await _AlertService.ShowErrorAsync(ex);
      await MainThread.InvokeOnMainThreadAsync(() => _NavigationService.GoToAsync("..", true));
      return;
    }
  }

  private static RelativeInfo[] AssembleRoots(
    PersonFullInfo personFullInfo,
    Parents parents,
    Siblings siblings,
    RelativeInfo[] stepChildren,
    IRelativesProvider relativesProvider)
  {
    var roots = new List<RelativeInfo>();
    void Add(IEnumerable<RelativeInfo> relatives) => roots.AddRange(relatives.OrderBy(r => r.BiologicalSex));

    Add(personFullInfo.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse));
    Add(parents.Native);
    Add(parents.Adoptive);
    Add(parents.Step);
    Add(siblings.Native);
    Add(siblings.ByFather);
    Add(siblings.ByMother);
    Add(siblings.Step);
    Add(siblings.Adoptive);
    Add(relativesProvider.GetChildren(personFullInfo.RelativeInfos));
    Add(relativesProvider.GetAdoptiveChildren(personFullInfo.RelativeInfos));
    Add(stepChildren);

    return [.. roots];
  }

  public void UpdateUI(PersonFullInfo personFullInfo,
                       RelativeInfo[] roots,
                       ImageSource[] photos,
                       string?[] captions,
                       IReadOnlyDictionary<int, string> mediaSources,
                       AttachmentInfo[] attachments,
                       string? bio,
                       string? gedcomDetails,
                       string? familyDetails,
                       bool addToNavigation)
  {
    _PersonFullInfo = personFullInfo;
    _Photos = photos;
    _Captions = captions;
    _MediaSources = mediaSources;
    _Attachments = attachments;
    _Biography = CombineBiography(bio, gedcomDetails, familyDetails);
    _AllRoots = roots;
    // Only after the new roots land: with the panel open, ResetFilterData re-fetches immediately,
    // snapshotting the page's current person set.
    FilterView.ResetFilterData();

    _Relatives.SetRoots(_AllRoots, _PersonFullInfo.BirthDate);

    this.RefreshView();

    if (addToNavigation)
    {
      AddToNavigation(_PersonFullInfo);
    }
  }

  private void AddToNavigation(PersonInfo personInfo)
  {
    var personInfoCopy = new PersonInfo(personInfo, names: personInfo.Names, mainPhoto: personInfo.MainPhoto);

    // Re-showing the person already selected in history (e.g. after editing it) must update that
    // entry in place; otherwise every edit/refresh of the current person pushes a duplicate.
    if (_NavigationIndex >= 0 && _NavigationHistory[_NavigationIndex].Id == personInfoCopy.Id)
    {
      _NavigationHistory[_NavigationIndex] = personInfoCopy;
      OnPropertyChanged(nameof(CurrentPerson));
      return;
    }

    var newIndex = _NavigationIndex + 1;
    while (newIndex < _NavigationHistory.Count)
    {
      _NavigationHistory.RemoveAt(newIndex);
    }
    _NavigationHistory.Add(personInfoCopy);

    // Not routed through the CurrentPerson setter: its data was just fetched by the caller of
    // AddToNavigation, so re-entering ShowPersonInfo here would re-run the whole fetch pipeline for
    // the same person a second time. OnNextPerson still goes through the setter, which does need a
    // fresh fetch for an already-visited (lightweight) history entry.
    _NavigationIndex = newIndex;
    OnPropertyChanged(nameof(CurrentPerson));
  }

  protected async Task OnPageCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "RemovePerson":
        await OnPersonRemoveAsync();
        break;
      case string commandName when commandName == "EditPerson":
        await OnPersonEditAsync();
        break;
      case string commandName when commandName == "Refresh":
        ShowPersonInfo(_PersonFullInfo, false);
        break;
      case string commandName when commandName == "GoToHome":
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<MainPage>());
        break;
      case string commandName when commandName == "GoToFamily":
        await OnGotoFamilyAsync();
        break;
      case string commandName when commandName == "GoToFamilyTree":
        // Shell matches the target's [QueryProperty] by exact runtime type, so hand it a plain
        // PersonInfo — passing the PersonFullInfo subclass sends Shell down a Convert.ChangeType path
        // that throws (the object is not IConvertible).
        var centerInfo = new PersonInfo(_PersonFullInfo, _PersonFullInfo.Names, _PersonFullInfo.MainPhoto);
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<FamilyTreePage>(), true, new() { ["PersonInfo"] = centerInfo });
        break;
      case RelativeInfo relativeInfo:
        ShowPersonInfo(relativeInfo, true);
        break;
      case AttachmentInfo attachment:
        await OnOpenAttachmentAsync(attachment);
        break;
      case string commandName when commandName == "PreviousPerson":
        OnNextPerson(-1);
        break;
      case string commandName when commandName == "NextPerson":
        OnNextPerson(1);
        break;
      case string commandName when commandName == "ToggleAll":
        ExpandAll = !ExpandAll;
        _ = SafeTask.Run(() => _Relatives.ExpandAllAsync(ExpandAll), _AlertService);
        break;
    }
  }

  private void OnNextPerson(int dIndex)
  {
    var person = _NavigationHistory.ElementAtOrDefault(_NavigationIndex + dIndex);
    if (person is not null)
    {
      CurrentPerson = person;
    }
  }

  private async Task OnPersonRemoveAsync()
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider
      .Project
      .Persons
      .RemovePersonAsync(_PersonFullInfo, token);

    await OnGotoFamilyAsync();
  }

  private async Task OnPersonEditAsync()
  {
    var dialog = _CreateOrUpdatePersonDialogFactory.Create(_PersonFullInfo);
    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    if (info is null)
    {
      return;
    }

    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    await _CurrentProjectProvider
      .Project
      .PersonManager
      .UpdatePersonAsync(info, token);

    PersonInfo = info;
  }
}

public sealed record AttachmentInfo(string FileName, string? Title, Data Data)
{
  public string DisplayName => string.IsNullOrWhiteSpace(Title) ? FileName : Title;

  public bool ShowFileName => !string.IsNullOrWhiteSpace(Title);
}
