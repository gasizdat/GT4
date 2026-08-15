using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Dto;
using GT4.UI.Utils.Formatters;
using System.ComponentModel;
using System.Windows.Input;

namespace GT4.UI.Items;

/// <summary>
/// One media row of the gallery: the stored <see cref="Data"/>, every person and family that links it,
/// and the caption to show for it. Title and Icon both come out of a single conversion -- an attachment
/// carries its filename inside the same residue envelope the thumbnail is unwrapped from, so resolving
/// them separately would parse it twice.
/// </summary>
public sealed class GalleryDataItem : CollectionItemBase<Data>, INotifyPropertyChanged
{
  private const string ExpandSymbol = "🔽";
  private const string CollapseSymbol = "­­­­⏫­";

  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterResolver;
  private ImageSource? _Icon;
  private string? _Caption;
  private bool _ContentRequested;
  private bool _ShowOwners;
  private string _MoreBtnName = ExpandSymbol;

  public GalleryDataItem(
    Data data,
    PersonInfo[] persons,
    Name[] families,
    INameFormatter nameFormatter,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver)
    : base(data, "project_icon.png")
  {
    Persons = persons;
    Families = families;
    PersonNames = [.. persons.Select(person => nameFormatter.ToString(person, NameFormat.CommonPersonName))];
    FamilyNames = [.. families.Select(family => family.Value)];
    Owners = string.Join(", ", PersonNames.Concat(FamilyNames));
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _DataConverterResolver = dataConverterResolver;
    ToggleOwnersCommand = new SafeCommand(OnToggleOwners, alertService);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public PersonInfo[] Persons { get; }

  public Name[] Families { get; }

  public string[] PersonNames { get; }

  public string[] FamilyNames { get; }

  public string Owners { get; }

  public bool HasOwners => PersonNames.Length + FamilyNames.Length > 0;

  public ICommand ToggleOwnersCommand { get; }

  /// <summary>The stored caption or filename, falling back to whoever owns the item.</summary>
  public string Title
  {
    get
    {
      RequestContent();
      return string.IsNullOrWhiteSpace(_Caption) ? Owners : _Caption;
    }
  }

  public override ImageSource Icon
  {
    get
    {
      RequestContent();
      return _Icon ?? base.Icon;
    }
  }

  public bool ShowOwners
  {
    get => _ShowOwners;
    private set
    {
      _ShowOwners = value;
      _MoreBtnName = value ? CollapseSymbol : ExpandSymbol;
      OnPropertyChanged(nameof(ShowOwners));
      OnPropertyChanged(nameof(MoreBtnName));
    }
  }

  public string MoreBtnName => _MoreBtnName;

  private void OnToggleOwners() => ShowOwners = !ShowOwners;

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
      });
    }

    SafeTask.Run(ResolveContentAsync, _AlertService);
  }

  private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
