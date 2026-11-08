using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Items;

public class PersonDataItem : DataItemBase
{
  private object? _Content = null;
  private bool _IsReady = false;
  private bool _ContentModified = false;
  private string? _CaptionOverride;
  private bool _CaptionModified = false;

  public PersonDataItem(Data data, IDataConverter dataConverter, ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService)
    : base(data, dataConverter, cancellationTokenProvider, alertService)
  {
  }

  public PersonDataItem(DataCategory dataCategory, IDataConverter dataConverter, ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService)
    : this(new Data(
               Id: ElementId.NonCommittedId,
               Content: [],
               MimeType: null,
               Category: dataCategory),
        dataConverter, cancellationTokenProvider, alertService)
  {
  }

  public object? Content
  {
    get
    {
      if (!_IsReady)
      {
        _IsReady = true;

        async Task UpdateContentAsync()
        {
          using var token = CancellationTokenProvider.CreateShortOperationCancellationToken();
          var content = await DataConverter.ToObjectAsync(Info, token);

          MainThread.BeginInvokeOnMainThread(() =>
          {
            _Content = content;
            OnContentChanged();
          });
        }

        SafeTask.Run(UpdateContentAsync, AlertService);
      }
      return _Content;
    }

    set
    {
      if (_Content != value)
      {
        _Content = value;
        _ContentModified = true;
        OnContentChanged();
      }
    }
  }

  public ImageSource? Image => ImageOf(Content);

  // Falls back to the decoded PhotoInfo.Caption/AttachmentInfo.Title once Content loads; the override
  // wins once set so a fast typist never loses input to a slower background decode.
  public string? Caption
  {
    get => _CaptionOverride ?? (Content as PhotoInfo)?.Caption ?? (Content as AttachmentInfo)?.Title;
    set
    {
      _CaptionOverride = value;
      _CaptionModified = true;
      OnPropertyChanged(nameof(Caption));
    }
  }

  public bool IsModified => _ContentModified || _CaptionModified;

  public async Task<Data?> ToDataAsync()
  {
    // Bypasses DataConverter entirely: it operates on Info's raw bytes, not the lazily-decoded Content,
    // so it never races the background load and never re-encodes the image.
    if (_CaptionModified)
    {
      using var captionToken = CancellationTokenProvider.CreateShortOperationCancellationToken();
      return await GedcomPhotoResidue.WithTitleAsync(Info, _CaptionOverride, captionToken);
    }

    // Unmodified items skip reconversion, which would be lossy for a tagged photo.
    if (!_ContentModified)
      return Info;

    using var token = CancellationTokenProvider.CreateShortOperationCancellationToken();
    var ret = await DataConverter.FromObjectAsync(_Content, token);
    if (ret is not null)
    {
      // A tagged Category coming back is the converter's signal that it re-encoded a residue envelope, and
      // the two must never disagree: the category alone picks the converter that unwraps, so an enveloped
      // Content stored as plain gets its tag bytes decoded as image bytes. Only main-vs-additional is ours.
      var category = Info.Category;
      if (ret.Category.IsTaggedPhoto())
        category = category.AsTaggedPhoto();
      else if (category.IsTaggedPhoto())
        category = category.AsPlainPhoto();

      ret = ret with { Id = ElementId.NonCommittedId, Category = category };
    }

    return ret;
  }

  public static async Task<Data[]> ToDataAsync(IEnumerable<PersonDataItem> items)
  {
    var conversions = items.Select(item => item.ToDataAsync());
    var data = await Task.WhenAll(conversions);
    return [.. data.OfType<Data>()];
  }

  private void OnContentChanged()
  {
    OnPropertyChanged(nameof(Content));
    OnPropertyChanged(nameof(Image));
    OnPropertyChanged(nameof(Caption));
  }
}
