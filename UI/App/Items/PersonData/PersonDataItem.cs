using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Converters;
using System.ComponentModel;

namespace GT4.UI.Items;

public class PersonDataItem : CollectionItemBase<Data>, INotifyPropertyChanged
{
  private readonly IDataConverter _DataConverter;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private object? _Content = null;
  private bool _IsReady = false;
  private bool _IsModified = false;

  private void OnContentChanged()
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
  }

  public PersonDataItem(Data data, IDataConverter dataConverter, ICancellationTokenProvider cancellationTokenProvider)
    : base(data, string.Empty)
  {
    _DataConverter = dataConverter;
    _CancellationTokenProvider = cancellationTokenProvider;
  }

  public PersonDataItem(DataCategory dataCategory, IDataConverter dataConverter, ICancellationTokenProvider cancellationTokenProvider)
    : this(new Data(
               Id: TableBase.NonCommitedId,
               Content: [],
               MimeType: null,
               Category: dataCategory),
        dataConverter, cancellationTokenProvider)
  {
    _DataConverter = dataConverter;
    _CancellationTokenProvider = cancellationTokenProvider;
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
          try
          {
            using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
            var content = await _DataConverter.ToObjectAsync(Info, token);

            MainThread.BeginInvokeOnMainThread(() =>
            {
              _Content = content;
              OnContentChanged();
            });
          }
          catch (Exception ex)
          {
            await PageAlert.ShowError(ex);
          }
        }

        Task.Run(UpdateContentAsync);
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

  public async Task<Data?> ToDataAsync()
  {
    using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
    var ret = await _DataConverter.FromObjectAsync(_Content, token);
    if (ret is not null)
    {
      var id = _IsModified ? TableBase.NonCommitedId : Info.Id;
      ret = ret with { Id = id, Category = Info.Category };
    }

    return ret;
  }

  public event PropertyChangedEventHandler? PropertyChanged;
}
