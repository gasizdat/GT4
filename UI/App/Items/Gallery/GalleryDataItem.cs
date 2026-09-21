using GT4.Core.Gedcom;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Dto;
using GT4.UI.Utils.Formatters;
using System.ComponentModel;

namespace GT4.UI.Items;

/// <summary>
/// One row of the project's media: the stored <see cref="Data"/>, every person and family that links it,
/// and the caption to show for it. Title and Icon both come out of a single conversion -- an attachment
/// carries its filename inside the same residue envelope the thumbnail is unwrapped from, so resolving
/// them separately would parse it twice.
/// </summary>
public sealed class GalleryDataItem : CollectionItemBase<Data>, INotifyPropertyChanged
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterResolver;
  private ImageSource? _Icon;
  private string? _Caption;
  private bool _ContentRequested;

  private GalleryDataItem(
    Data data,
    PersonInfo[] persons,
    Name[] families,
    string[] personNames,
    string[] familyNames,
    string owners,
    string sortTitle,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver)
    : base(data, "project_icon.png")
  {
    Persons = persons;
    Families = families;
    PersonNames = personNames;
    FamilyNames = familyNames;
    Owners = owners;
    SortTitle = sortTitle;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _DataConverterResolver = dataConverterResolver;
  }

  /// <summary>Resolves the sort/filter title up front -- from the residue's own TITL/FILE metadata rather
  /// than a full conversion -- so every item is immediately consistent instead of carrying an Owners
  /// fallback until something later calls a separate resolve step.</summary>
  private static async Task<GalleryDataItem> CreateAsync(
    Data data,
    PersonInfo[] persons,
    Name[] families,
    INameFormatter nameFormatter,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver,
    CancellationToken token)
  {
    string[] personNames = [.. persons.Select(person => nameFormatter.ToString(person, NameFormat.CommonPersonName))];
    string[] familyNames = [.. families.Select(family => family.Value)];
    var owners = string.Join(", ", personNames.Concat(familyNames));

    var title = await GedcomPhotoResidue.ExtractTitleAsync(data, token);
    if (string.IsNullOrWhiteSpace(title) && data.Category.IsAttachment())
    {
      title = await GedcomPhotoResidue.ExtractFileNameAsync(data, token);
    }
    var sortTitle = string.IsNullOrWhiteSpace(title) ? owners : title;

    return new(
      data, persons, families, personNames, familyNames, owners, sortTitle,
      cancellationTokenProvider, alertService, dataConverterResolver);
  }

  /// <summary>
  /// Every photo and attachment in the project, each joined with the persons and families that link it.
  /// Both the sweep and the two reverse lookups are project-wide, so this holds every media blob at once
  /// -- the eager materialization of #158, one sweep at a time. Callers order the result themselves.
  /// </summary>
  public static async Task<GalleryDataItem[]> LoadProjectMediaAsync(
    IProjectDocument project,
    INameFormatter nameFormatter,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver,
    CancellationToken token)
  {
    var dataSet = await project.Data.GetDataSetAsync(token);
    var personIdsByData = await project.PersonData.GetPersonIdsByDataAsync(token);
    var nameIdsByData = await project.NameData.GetNameIdsByDataAsync(token);
    var persons = await project.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, token);
    var names = await project.Names.GetNamesByTypeAsync(NameType.AllNames, token);

    var personsById = persons.ToDictionary(person => person.Id);
    var namesById = names.ToDictionary(name => name.Id);

    Task<GalleryDataItem> CreateItemAsync(Data media)
    {
      var ownerPersonIds = personIdsByData.GetValueOrDefault(media.Id) ?? [];
      var ownerNameIds = nameIdsByData.GetValueOrDefault(media.Id) ?? [];
      PersonInfo[] owningPersons = [.. ownerPersonIds.Select(id => personsById[id])];
      Name[] owningFamilies = [.. ownerNameIds.Select(id => namesById[id])];

      return CreateAsync(
        media, owningPersons, owningFamilies, nameFormatter,
        cancellationTokenProvider, alertService, dataConverterResolver, token);
    }

    var itemTasks = dataSet
      .Where(media => media.Category.IsPhoto() || media.Category.IsAttachment())
      .Select(CreateItemAsync);

    return await Task.WhenAll(itemTasks);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public PersonInfo[] Persons { get; }

  public Name[] Families { get; }

  public string[] PersonNames { get; }

  public string[] FamilyNames { get; }

  public string Owners { get; }

  public bool HasOwners => PersonNames.Length + FamilyNames.Length > 0;

  /// <summary>Whether owners are worth a line of their own: Title already shows them when there is no caption.</summary>
  public bool HasDistinctOwners => HasOwners && !string.IsNullOrWhiteSpace(_Caption);

  /// <summary>The stored caption or filename, falling back to whoever owns the item.</summary>
  public string Title
  {
    get
    {
      RequestContent();
      return string.IsNullOrWhiteSpace(_Caption) ? Owners : _Caption;
    }
  }

  /// <summary>Same fallback as <see cref="Title"/>, but resolved from the residue's own metadata rather
  /// than a full conversion -- cheap enough to have resolved for every item up front, to sort and filter
  /// by what the row will display instead of by <see cref="Owners"/> alone.</summary>
  public string SortTitle { get; }

  /// <summary>Deliberately not <see cref="Title"/> -- reading it would decode every blob in the project on
  /// the first keystroke.</summary>
  public bool MatchesFilter(string filter) =>
    Owners.Contains(filter, StringComparison.InvariantCultureIgnoreCase) ||
    SortTitle.Contains(filter, StringComparison.InvariantCultureIgnoreCase);

  public override ImageSource Icon
  {
    get
    {
      RequestContent();
      return _Icon ?? base.Icon;
    }
  }

  private void RequestContent()
  {
    if (_ContentRequested)
    {
      return;
    }
    _ContentRequested = true;

    async Task ResolveContentAsync()
    {
      using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
      var converter = _DataConverterResolver(Info.Category);
      // The converter yields the thumbnail already downsized; a file that renders as nothing (a document)
      // still comes back carrying the caption, which is the half of the conversion that always applies.
      var thumbnail = new ImageDataWithMaxSize(Info, ImageUtils.ThumbnailSize);
      var content = await converter.ToObjectAsync(thumbnail, token);
      var source = content switch
      {
        PhotoInfo photo => photo.Source,
        AttachmentInfo attachment => attachment.Image?.Source,
        _ => null
      };
      var caption = content switch
      {
        PhotoInfo photo => photo.Caption,
        AttachmentInfo attachment => attachment.DisplayName,
        _ => null
      };

      MainThread.BeginInvokeOnMainThread(() =>
      {
        _Icon = source;
        _Caption = caption;
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasDistinctOwners));
      });
    }

    SafeTask.Run(ResolveContentAsync, _AlertService);
  }

  private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
