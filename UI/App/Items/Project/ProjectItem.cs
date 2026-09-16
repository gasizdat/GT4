using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Dto;
using GT4.UI.Utils.Formatters;
using System.ComponentModel;

namespace GT4.UI.Items;

public class ProjectItem : CollectionItemBase<ProjectInfo>, INotifyPropertyChanged
{
  private readonly PersonInfo? _MainPerson;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterResolver;
  private ImageSource? _MainPersonPhoto;
  private bool _MainPersonPhotoRequested;

  public ProjectItem(
    ProjectInfo info,
    PersonInfo? mainPerson,
    INameFormatter nameFormatter,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver)
    : base(info, "project_icon.png")
  {
    _MainPerson = mainPerson;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _DataConverterResolver = dataConverterResolver;
    MainPersonName = mainPerson is null
      ? string.Empty
      : string.Format(UIStrings.FieldMainPerson_1, nameFormatter.ToString(mainPerson, NameFormat.CommonPersonName));
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public string Description => Info.Description;

  public string Name => Info.Name;

  // The counter starts at 1, so null or the initial value means "no revision".
  public string Revision => Info.Revision is null or ProjectInfo.InitialRevision
    ? string.Empty
    : string.Format(UIStrings.FieldRevision_1, Info.Revision);

  public string MainPersonName { get; }

  public bool DescriptionVisible => !string.IsNullOrWhiteSpace(Description);

  public bool RevisionVisible => !string.IsNullOrWhiteSpace(Revision);

  public bool MainPersonVisible => _MainPerson is not null;

  public ImageSource? MainPersonPhoto
  {
    get
    {
      RequestMainPersonPhoto();
      return _MainPersonPhoto;
    }
  }

  private void RequestMainPersonPhoto()
  {
    if (_MainPersonPhotoRequested || _MainPerson?.MainPhoto is not { } mainPhoto)
    {
      return;
    }
    _MainPersonPhotoRequested = true;

    async Task UpdatePhotoAsync()
    {
      using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
      var converter = _DataConverterResolver(mainPhoto.Category);
      var thumbnail = new ImageDataWithMaxSize(mainPhoto, ImageUtils.ThumbnailSize);
      var photo = await converter.ToObjectAsync(thumbnail, token) is PhotoInfo photoInfo ? photoInfo.Source : null;

      MainThread.BeginInvokeOnMainThread(() =>
      {
        _MainPersonPhoto = photo;
        OnPropertyChanged(nameof(MainPersonPhoto));
      });
    }

    SafeTask.Run(UpdatePhotoAsync, _AlertService);
  }

  private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
