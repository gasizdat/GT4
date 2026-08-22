using GT4.Core.Project.Abstraction;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

/// <summary>
/// Offers every photo and attachment in the project as a biography link target, the media of the person
/// being edited first. A biography link is display-only -- it re-exports inside the person's NOTE and has
/// no OBJE of its own -- so a foreign item is no less linkable than an own one (issue #300).
/// </summary>
public partial class SelectMediaDialog : ContentPage
{
  public record class Factory(
    ICancellationTokenProvider CancellationTokenProvider,
    ICurrentProjectProvider CurrentProjectProvider,
    INameFormatter NameFormatter,
    IAlertService AlertService,
    DataConverterResolver DataConverterResolver)
  {
    public SelectMediaDialog Create(int[] ownMediaIds) => new SelectMediaDialog(this, ownMediaIds);
  }

  private readonly Factory _Factory;
  private readonly int[] _OwnMediaIds;
  private readonly FilteredObservableCollection<GalleryDataItem> _Items = new();
  private readonly TaskCompletionSource<GalleryDataItem?> _Info = new(null);
  private readonly ICommand _DialogCommand;
  private bool _LoadItems = true;
  private string _OwnerFilter = string.Empty;
  private GalleryDataItem? _SelectedItem;

  public SelectMediaDialog(Factory factory, int[] ownMediaIds)
  {
    _Factory = factory;
    _OwnMediaIds = ownMediaIds;
    _DialogCommand = new SafeCommand(OnDialogCommand, factory.AlertService);
    _Items.Filter = OwnersFilter;

    InitializeComponent();
  }

  public IEnumerable<GalleryDataItem> Items
  {
    get
    {
      async Task AddMediaItemsAsync()
      {
        using var token = _Factory.CancellationTokenProvider.CreateDbCancellationToken();
        var media = await GalleryDataItem.LoadProjectMediaAsync(
          _Factory.CurrentProjectProvider.Project,
          _Factory.NameFormatter,
          _Factory.CancellationTokenProvider,
          _Factory.AlertService,
          _Factory.DataConverterResolver,
          token);

        var ownMediaIds = _OwnMediaIds.ToHashSet();
        var items = media
          .OrderByDescending(item => ownMediaIds.Contains(item.Info.Id))
          .ThenBy(item => item.Owners, StringComparer.CurrentCulture)
          .ThenBy(item => item.Info.Id);

        MainThread.BeginInvokeOnMainThread(() => _Items.AddRange(items));
      }

      // Latched before the load starts, not off _Items.Count: the count is the filtered one, and a
      // second getter call while the first load is still in flight would append the sweep twice.
      if (_LoadItems)
      {
        _LoadItems = false;
        LayoutView.RunLoad(_Items.Count != 0, AddMediaItemsAsync, _Factory.AlertService);
      }

      return _Items.Items;
    }
  }

  // Owners rather than Title: a row's title comes out of a conversion the item defers until something
  // renders it, so filtering on it would decode every blob in the project on the first keystroke.
  public string OwnerFilter
  {
    get => _OwnerFilter;
    set
    {
      _OwnerFilter = value;
      _Items.Update();
    }
  }

  public GalleryDataItem? SelectedItem
  {
    get => _SelectedItem;
    set
    {
      _SelectedItem = value;
      OnPropertyChanged(nameof(SelectedItem));
      OnPropertyChanged(nameof(DialogButtonName));
    }
  }

  public string DialogButtonName => _SelectedItem is not null ? UIStrings.BtnNameOk : UIStrings.BtnNameCancel;

  public Task<GalleryDataItem?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  protected Task OnDialogCommand(object obj)
  {
    if (obj is string commandName && commandName == "SelectMediaCommand")
    {
      _Info.SetResult(_SelectedItem);
    }

    return Task.CompletedTask;
  }

  private bool OwnersFilter(FilteredObservableCollection<GalleryDataItem> collection, GalleryDataItem item) =>
    string.IsNullOrEmpty(_OwnerFilter) ||
    item.Owners.Contains(_OwnerFilter, StringComparison.InvariantCultureIgnoreCase);
}
