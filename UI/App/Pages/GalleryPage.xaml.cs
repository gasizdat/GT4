using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Pages;

/// <summary>
/// Lists every photo and attachment in the project against the persons and families that link it.
/// Composed in memory from the per-owner data sets rather than from a project-wide query, which has two
/// consequences: the listing holds every media blob of the project at once (the eager materialization of
/// #158, project-wide), and a Data row no owner links is unreachable here, so it is not shown.
/// </summary>
public partial class GalleryPage : ContentPage
{
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly INameFormatter _NameFormatter;
  private readonly IAlertService _AlertService;
  private readonly DataConverterResolver _DataConverterResolver;
  private readonly FilteredObservableCollection<GalleryDataItem> _Items = new();
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
    _Items.Filter = OwnersFilter;

    InitializeComponent();
  }

  private bool OwnersFilter(FilteredObservableCollection<GalleryDataItem> _, GalleryDataItem item) =>
    string.IsNullOrEmpty(_OwnerFilter) ||
    item.Owners.Contains(_OwnerFilter, StringComparison.InvariantCultureIgnoreCase);

  public IEnumerable<GalleryDataItem> Items
  {
    get
    {
      async Task AddGalleryItemsAsync()
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var project = _CurrentProjectProvider.Project;
        var persons = await project
          .PersonManager
          .GetPersonInfosAsync(selectMainPhoto: false, token);
        var personData = await project
          .PersonData
          .GetPersonDataSetAsync(persons, null, token);
        var families = await project
          .Names
          .GetNamesByTypeAsync(NameType.FamilyName, token);
        var familyData = await project
          .NameData
          .GetNameDataSetAsync(families, null, token);

        var media = new Dictionary<int, Data>();
        var owners = new Dictionary<int, List<string>>();

        void Collect(Data data, string owner)
        {
          if (!data.Category.IsPhoto() && !data.Category.IsAttachment())
          {
            return;
          }

          media[data.Id] = data;
          if (!owners.TryGetValue(data.Id, out var ownerNames))
          {
            owners[data.Id] = ownerNames = [];
          }
          ownerNames.Add(owner);
        }

        GalleryDataItem CreateItem(Data data)
        {
          var ownerNames = string.Join(", ", owners[data.Id]);
          return new(data, ownerNames, _CancellationTokenProvider, _AlertService, _DataConverterResolver);
        }

        foreach (var person in persons)
        {
          var personName = _NameFormatter.ToString(person, NameFormat.CommonPersonName);
          foreach (var data in personData.GetValueOrDefault(person.Id) ?? [])
          {
            Collect(data, personName);
          }
        }

        foreach (var family in families)
        {
          foreach (var data in familyData.GetValueOrDefault(family.Id) ?? [])
          {
            Collect(data, family.Value);
          }
        }

        var items = media
          .Values
          .Select(CreateItem)
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
