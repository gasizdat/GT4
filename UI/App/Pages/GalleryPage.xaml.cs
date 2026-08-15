using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Pages;

/// <summary>
/// Lists every photo and attachment in the project against the persons and families that link it. The
/// listing starts from the Data table rather than from its owners, so a row nothing links still shows up
/// (and can be deleted from here).
/// </summary>
public partial class GalleryPage : ContentPage
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly INameFormatter _NameFormatter;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterResolver;
  private readonly FilteredObservableCollection<GalleryDataItem> _Items = new();
  private readonly ICommand _DeleteDataCommand;
  private readonly ICommand _OpenDataCommand;
  private bool _UpdateItems = true;
  private string _OwnerFilter = string.Empty;

  public GalleryPage(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    INameFormatter nameFormatter,
    IAlertService alertService,
    DataConverterResolver dataConverterResolver
    )
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _NameFormatter = nameFormatter;
    _AlertService = alertService;
    _DataConverterResolver = dataConverterResolver;
    _DeleteDataCommand = new SafeCommand(OnDeleteCommandAsync, _AlertService);
    _OpenDataCommand = new SafeCommand(OnOpenCommandAsync, _AlertService);
    _Items.Filter = OwnersFilter;

    InitializeComponent();
  }

  protected async Task OnDeleteCommandAsync(object obj)
  {
    if (obj is GalleryDataItem item)
    {
      var project = _CurrentProjectProvider.Project;
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      using var transaction = await project.BeginTransactionAsync(token);

      foreach (var person in item.Persons)
      {
        await project.PersonData.RemovePersonDataAsync(person, item.Info, token);
      }
      foreach (var family in item.Families)
      {
        await project.NameData.RemoveNameDataAsync(family, item.Info, token);
      }
      // Dropping the last link already takes the row with it; an item nothing links has no such link.
      await project.Data.RemoveDataAsync(item.Info, token);
      await transaction.CommitAsync(token);

      RequestUpdateItems();
    }
  }

  protected async Task OnOpenCommandAsync(object obj)
  {
    if (obj is GalleryDataItem item)
    {
      using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();
      // Converted without a size cap, unlike the row's thumbnail: this is the full-resolution open.
      var converter = _DataConverterResolver(item.Info.Category);
      var content = await converter.ToObjectAsync(item.Info, token);

      switch (content)
      {
        case AttachmentInfo attachment:
          await attachment.OpenAsync(token);
          break;

        case PhotoInfo photo:
          await Navigation.PushModalAsync(new PhotoViewerDialog([photo.Source], _AlertService));
          break;
      }
    }
  }

  protected void RequestUpdateItems()
  {
    _UpdateItems = true;
    OnPropertyChanged(nameof(Items));
  }

  private bool OwnersFilter(FilteredObservableCollection<GalleryDataItem> _, GalleryDataItem item) =>
    string.IsNullOrEmpty(_OwnerFilter) ||
    item.Owners.Contains(_OwnerFilter, StringComparison.InvariantCultureIgnoreCase);

  public ICommand DeleteDataCommand => _DeleteDataCommand;

  public ICommand OpenDataCommand => _OpenDataCommand;

  public IEnumerable<GalleryDataItem> Items
  {
    get
    {
      async Task AddGalleryItemsAsync()
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var media = await GalleryDataItem.LoadProjectMediaAsync(
          _CurrentProjectProvider.Project,
          _NameFormatter,
          _CancellationTokenProvider,
          _AlertService,
          _DataConverterResolver,
          token);

        var items = media
          .OrderBy(item => item.Owners, StringComparer.CurrentCulture)
          .ThenBy(item => item.Info.Id)
          .ToArray();

        MainThread.BeginInvokeOnMainThread(() =>
        {
          _Items.Clear();
          _Items.AddRange(items);
        });
      }

      if (_UpdateItems)
      {
        _UpdateItems = false;
        _ = SafeTask.Run(AddGalleryItemsAsync, _AlertService);
      }

      return _Items.Items;
    }
  }

  public string OwnerFilter
  {
    get => _OwnerFilter;
    set
    {
      _OwnerFilter = value;
      _Items.Update();
    }
  }
}
