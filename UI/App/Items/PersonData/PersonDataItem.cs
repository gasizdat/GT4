using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Utils.Converters;
using System.ComponentModel;

namespace GT4.UI.Items;

public class PersonDataItem : CollectionItemBase<Data>, INotifyPropertyChanged
{
  private readonly IDataConverter _DataConverter;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private object? _Content = null;
  private bool _IsReady = false;
  private bool _IsModified = false;

  private void OnContentChanged()
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
  }

  public PersonDataItem(Data data, IDataConverter dataConverter, ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService)
    : base(data, string.Empty)
  {
    _DataConverter = dataConverter;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
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
    _DataConverter = dataConverter;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
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
          using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
          var content = await _DataConverter.ToObjectAsync(Info, token);

          MainThread.BeginInvokeOnMainThread(() =>
          {
            _Content = content;
            OnContentChanged();
          });
        }

        SafeTask.Run(UpdateContentAsync, _AlertService);
      }
      return _Content;
    }

    set
    {
      if (_Content != value)
      {
        _Content = value;
        _IsModified = true;
        OnContentChanged();
      }
    }
  }

  public bool IsModified => _IsModified;

  public async Task<Data?> ToDataAsync()
  {
    // Unmodified items skip reconversion, which would be lossy for a tagged photo.
    if (!_IsModified)
      return Info;

    using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
    var ret = await _DataConverter.FromObjectAsync(_Content, token);
    if (ret is not null)
    {
      // A modified photo downgrades tagged -> plain; non-photo categories are untouched.
      var category = Info.Category.IsPhoto() ? Info.Category.AsPlainPhoto() : Info.Category;
      ret = ret with { Id = ElementId.NonCommittedId, Category = category };
    }

    return ret;
  }

  public event PropertyChangedEventHandler? PropertyChanged;
}
