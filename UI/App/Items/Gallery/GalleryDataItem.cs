using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Dto;
using System.ComponentModel;

namespace GT4.UI.Items;

public sealed class GalleryDataItem : CollectionItemBase<Data>, INotifyPropertyChanged
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterResolver;
  private ImageSource? _Icon;
  private bool _IconReady;

  public GalleryDataItem(
    Data data,
    string owners,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver)
    : base(data, "project_icon.png")
  {
    Owners = owners;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _DataConverterResolver = dataConverterResolver;
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public string Owners { get; }

  public override ImageSource Icon
  {
    get
    {
      if (!Info.IsInlineImage())
      {
        return base.Icon;
      }

      if (!_IconReady)
      {
        _IconReady = true;

        async Task UpdateIconAsync()
        {
          using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
          var converter = _DataConverterResolver(Info.Category);
          var thumbnail = new ImageDataWithMaxSize(Info, ImageUtils.ThumbnailSize);
          var content = await converter.ToObjectAsync(thumbnail, token);
          var source = content switch
          {
            PhotoInfo photo => photo.Source,
            AttachmentInfo attachment => attachment.Image?.Source,
            _ => null
          };

          MainThread.BeginInvokeOnMainThread(() =>
          {
            _Icon = source;
            OnPropertyChanged(nameof(Icon));
          });
        }

        SafeTask.Run(UpdateIconAsync, _AlertService);
      }

      return _Icon ?? base.Icon;
    }
  }

  private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
