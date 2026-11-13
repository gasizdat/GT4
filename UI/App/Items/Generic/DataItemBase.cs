using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Dto;
using System.ComponentModel;

namespace GT4.UI.Items;

public abstract class DataItemBase : CollectionItemBase<Data>, INotifyPropertyChanged
{
  private ImageSource? _Thumbnail;
  private bool _ThumbnailRequested = false;

  protected DataItemBase(Data data, IDataConverter dataConverter, ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService)
    : base(data, string.Empty)
  {
    DataConverter = dataConverter;
    CancellationTokenProvider = cancellationTokenProvider;
    AlertService = alertService;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  /// <summary>Null only for a file that isn't an image: a document gets no picture at all.</summary>
  public ImageSource? Thumbnail
  {
    get
    {
      RequestThumbnail();
      return _Thumbnail;
    }
  }

  public bool HasThumbnail => Thumbnail is not null;

  protected IDataConverter DataConverter { get; }

  protected ICancellationTokenProvider CancellationTokenProvider { get; }

  protected IAlertService AlertService { get; }

  protected static ImageSource? ImageOf(object? content) => content switch
  {
    PhotoInfo photo => photo.Source,
    AttachmentInfo attachment => attachment.Image,
    _ => null
  };

  protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  private void RequestThumbnail()
  {
    if (_ThumbnailRequested)
    {
      return;
    }
    _ThumbnailRequested = true;

    async Task ResolveThumbnailAsync()
    {
      using var token = CancellationTokenProvider.CreateShortOperationCancellationToken();
      var thumbnail = new ImageDataWithMaxSize(Info, ImageUtils.ThumbnailSize);
      var content = await DataConverter.ToObjectAsync(thumbnail, token);
      var source = ImageOf(content);

      MainThread.BeginInvokeOnMainThread(() =>
      {
        _Thumbnail = source;
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
      });
    }

    SafeTask.Run(ResolveThumbnailAsync, AlertService);
  }
}
