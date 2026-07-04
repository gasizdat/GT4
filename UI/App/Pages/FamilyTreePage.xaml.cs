using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components.Genealogy;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Settings;
using Microsoft.Maui.Layouts;
using System.Collections.Concurrent;
using System.Windows.Input;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class FamilyTreePage : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly INameFormatter _NameFormatter;
  private readonly FamilyTreeLayoutMetrics _Metrics = new();
  private readonly FamilyTreeLayout _Layout = new();
  private readonly Color _ParentChildColor;
  private readonly Color _SpouseColor;
  private readonly FontScale? _FontScale;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  // Reused across loads so an incremental "load more" updates the existing canvas instead of rebuilding
  // every node view; views that do leave the tree are disconnected to release their native resources.
  private readonly Dictionary<int, NodeEntry> _NodeCache = [];
  private readonly List<Path> _ConnectorPool = [];
  // Small per-person photo thumbnails, decoded once off the UI thread; empty = decode failed, use stub.
  // Concurrent because overlapping loads (e.g. zoom while a load is in flight) decode into it in parallel.
  private readonly ConcurrentDictionary<int, byte[]> _ThumbnailCache = new();
  private const float PhotoThumbnailSize = 200;
  private const double ConnectorLineWidth = 2;
  // Each "load more" click adds GenerationsPerLoad generations, up to this hard ceiling.
  private const int GenerationsPerLoad = 3;
  private const int MaxGenerations = 120;
  private const int InitialGenerations = 2;
  private const double MinZoom = 0.4;
  private const double MaxZoom = 2.5;
  private const double ZoomStep = 0.25;

  private Person? _Center;
  private string _CenterName = string.Empty;
  private int _AncestorGenerations = InitialGenerations;
  private int _DescendantGenerations = InitialGenerations;
  private bool _IncludeCollaterals = false;
  private bool _CanLoadMoreAncestors;
  private bool _CanLoadMoreDescendants;
  private ViewTarget _ViewTarget = ViewTarget.Center;
  private double _PanStartScrollX;
  private double _PanStartScrollY;
  private double _ZoomScale = 1.0;
  private int _LoadOperationsCount = 0;

  // Where to park the viewport after a (re)build.
  private enum ViewTarget { Center, Top, Bottom }

  // A cached node view plus the size/centre state it was built for, so it can be reused while those hold.
  private sealed record NodeEntry(FamilyTreeNodeView View, double Zoom, bool IsCenter);

  public FamilyTreePage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    INameFormatter nameFormatter,
    FontScale? fontScale,
    IAlertService alertService,
    INavigationService navigationService
  )
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _NameFormatter = nameFormatter;
    _FontScale = fontScale;
    _AlertService = alertService;
    _NavigationService = navigationService;
    PageCommand = new SafeCommand(OnPageCommand, _AlertService);

    InitializeComponent();

    _ParentChildColor = GetColor("Primary", Color.FromArgb("#1E4437"));
    _SpouseColor = GetColor("Accent", Color.FromArgb("#8B6F4E"));

    // Drag-to-pan: the ScrollView already handles wheel, scrollbars and touch flicks, but desktop
    // users expect to grab the canvas and drag it. Translate the pan delta into a scroll offset.
    var pan = new PanGestureRecognizer();
    pan.PanUpdated += OnCanvasPan;
    Canvas.GestureRecognizers.Add(pan);

#if DEBUG
    AddDiagnosticToolbarItems();
#endif
  }

#if DEBUG
  // Diagnostic-only affordances for stress-testing deep-tree rendering; never shipped in Release.
  private void AddDiagnosticToolbarItems()
  {
    ToolbarItems.Add(new ToolbarItem { Text = "Load deep (diag)", Order = ToolbarItemOrder.Secondary, Command = PageCommand, CommandParameter = "LoadDeep" });
    ToolbarItems.Add(new ToolbarItem { Text = "Auto-load incremental (diag)", Order = ToolbarItemOrder.Secondary, Command = PageCommand, CommandParameter = "AutoLoad" });
  }
#endif

  private void OnCanvasPan(object? sender, PanUpdatedEventArgs e)
  {
    switch (e.StatusType)
    {
      case GestureStatus.Started:
        _PanStartScrollX = Scroller.ScrollX;
        _PanStartScrollY = Scroller.ScrollY;
        break;

      case GestureStatus.Running:
        // Dragging the content right (positive TotalX) should reveal content to the left, i.e. reduce
        // the scroll offset — hence the subtraction. Clamp so we never scroll past the canvas edges.
        var maxX = Math.Max(0, Canvas.Width - Scroller.Width);
        var maxY = Math.Max(0, Canvas.Height - Scroller.Height);
        var targetX = Math.Clamp(_PanStartScrollX - e.TotalX, 0, maxX);
        var targetY = Math.Clamp(_PanStartScrollY - e.TotalY, 0, maxY);
        _ = Scroller.ScrollToAsync(targetX, targetY, animated: false);
        break;
    }
  }

  public ICommand PageCommand { get; }

  public PersonInfo PersonInfo
  {
    set => SetCenter(value);
  }

  public string PageTitle => string.Format(UIStrings.TitleFamilyTreePage_1, _CenterName);

  public string OpenPersonToolbarItemName => string.Format(UIStrings.MenuItemNameOpenPerson_1, _CenterName);

  // Drive the visibility of the top/bottom "load more" buttons.
  public bool CanLoadMoreAncestors
  {
    get => _CanLoadMoreAncestors && !LoadInProgress;
    private set
    {
      if (_CanLoadMoreAncestors == value)
      {
        return;
      }

      _CanLoadMoreAncestors = value;
      OnPropertyChanged(nameof(CanLoadMoreAncestors));
    }
  }

  public bool CanLoadMoreDescendants
  {
    get => _CanLoadMoreDescendants && !LoadInProgress;
    private set
    {
      if (_CanLoadMoreDescendants == value)
      {
        return;
      }

      _CanLoadMoreDescendants = value;
      OnPropertyChanged(nameof(CanLoadMoreDescendants));
    }
  }

  public bool IncludeCollaterals
  {
    get => _IncludeCollaterals;
    set
    {
      if (_IncludeCollaterals == value)
      {
        return;
      }

      _IncludeCollaterals = value;
      OnPropertyChanged(nameof(IncludeCollaterals));
      Reload(ViewTarget.Center);
    }
  }

  public bool LoadInProgress
  {
    get => _LoadOperationsCount != 0;
  }

  private void SetLoadInProgress()
  {
    _LoadOperationsCount++;

    OnPropertyChanged(nameof(LoadInProgress));
    OnPropertyChanged(nameof(CanLoadMoreAncestors));
    OnPropertyChanged(nameof(CanLoadMoreDescendants));
  }

  private void ResetLoadInProgress()
  {
    _LoadOperationsCount = Math.Max(_LoadOperationsCount - 1, 0);

    OnPropertyChanged(nameof(LoadInProgress));
    OnPropertyChanged(nameof(CanLoadMoreAncestors));
    OnPropertyChanged(nameof(CanLoadMoreDescendants));
  }

  private void SetCenter(PersonInfo person)
  {
    _Center = person;
    _CenterName = _NameFormatter.ToString(person, NameFormat.ShortPersonName);
    // A new centre starts a fresh view, so reset the depth and all stored layout positions.
    _AncestorGenerations = InitialGenerations;
    _DescendantGenerations = InitialGenerations;
    _Layout.Reset();
    OnPropertyChanged(nameof(PageTitle));
    OnPropertyChanged(nameof(OpenPersonToolbarItemName));
    Reload(ViewTarget.Center);
  }

  private void Reload(ViewTarget target)
  {
    if (_Center is null)
    {
      return;
    }

    _ViewTarget = target;
    var center = _Center;
    SetLoadInProgress();
    _ = SafeTask.Run(() => LoadAsync(center), _AlertService);
  }

  private async Task LoadAsync(Person center)
  {
    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var tree = await _CurrentProjectProvider
        .Project
        .FamilyTreeProvider
        .BuildAsync(center, _AncestorGenerations, _DescendantGenerations, _IncludeCollaterals, token);

      var zoom = _ZoomScale;
      var scaledMetrics = _Metrics with
      {
        NodeWidth = _Metrics.NodeWidth * zoom,
        NodeHeight = _Metrics.NodeHeight * zoom,
        HorizontalGap = _Metrics.HorizontalGap * zoom,
        VerticalGap = _Metrics.VerticalGap * zoom,
        Margin = _Metrics.Margin * zoom,
        CornerRadius = _Metrics.CornerRadius * zoom,
      };

      var layout = _Layout.Update(tree, scaledMetrics);
      CacheThumbnails(tree);
      var names = layout.Nodes.ToDictionary(
        node => node.Node.Id,
        node => _NameFormatter.ToString(node.Node.Person, NameFormat.ShortPersonName));

      await SafeTask.RunOnMainThread(() => Render(tree.CenterId, layout, names, zoom), _AlertService);
    }
    finally
    {
      await SafeTask.RunOnMainThread(ResetLoadInProgress, _AlertService);
    }
  }

  private void Render(int centerId, FamilyTreeLayoutResult layout, IReadOnlyDictionary<int, string> names, double zoom)
  {
#if DEBUG
    // Diagnostic: split the "Layout cycle" crash between a content-size/texture limit and memory
    // exhaustion. privateMB >> managedMB means unmanaged bitmaps/surfaces (decoded photos / GPU).
    var process = System.Diagnostics.Process.GetCurrentProcess();
    var managedMb = GC.GetTotalMemory(false) / (1024 * 1024);
    var privateMb = process.PrivateMemorySize64 / (1024 * 1024);
    System.Diagnostics.Debug.WriteLine(
      $"[FamilyTree] nodes={layout.Nodes.Count} canvas={layout.CanvasSize.Width:F0}x{layout.CanvasSize.Height:F0} " +
      $"ancGen={_AncestorGenerations} density={DeviceDisplay.Current.MainDisplayInfo.Density:F2} " +
      $"managedMB={managedMb} privateMB={privateMb}");
#endif

    // Reuse the connector shapes and node views across loads instead of clearing and rebuilding the
    // whole canvas: an incremental "load more" only adds the new generation's elements rather than
    // recreating hundreds of node views and connector shapes every time. Elements that do leave the
    // tree (surplus connectors, evicted nodes) are disconnected to release their native resources.
    UpdateConnectors(layout.Connectors, zoom);
    UpdateNodes(layout.Nodes, centerId, names, zoom);

    // A "load more" button is offered only while the tree actually reaches the requested depth: if a
    // generation came back shorter than asked for, that direction has no more data to fetch.
    var maxGeneration = layout.Nodes.Count == 0 ? 0 : layout.Nodes.Max(node => node.Node.Generation);
    var minGeneration = layout.Nodes.Count == 0 ? 0 : layout.Nodes.Min(node => node.Node.Generation);
    CanLoadMoreAncestors = _AncestorGenerations < MaxGenerations && maxGeneration >= _AncestorGenerations;
    CanLoadMoreDescendants = _DescendantGenerations < MaxGenerations && minGeneration <= -_DescendantGenerations;

    Canvas.WidthRequest = layout.CanvasSize.Width;
    Canvas.HeightRequest = layout.CanvasSize.Height;

    _ = PositionViewportAsync(layout.CenterTopLeft, zoom);
  }

  // Connectors carry no stable identity (they are redrawn from scratch each layout), so a simple index
  // pool is enough: reuse the first N shapes in place, create any extra, drop the surplus.
  private void UpdateConnectors(IReadOnlyList<FamilyTreeConnector> connectors, double zoom)
  {
    var cornerRadius = _Metrics.CornerRadius * zoom;
    for (var i = 0; i < connectors.Count; i++)
    {
      var connector = connectors[i];
      var color = connector.Relation == FamilyTreeRelation.Spouse ? _SpouseColor : _ParentChildColor;
      if (i < _ConnectorPool.Count)
      {
        FamilyTreeConnectorShape.Update(_ConnectorPool[i], connector, cornerRadius, ConnectorLineWidth, color);
      }
      else
      {
        var path = FamilyTreeConnectorShape.Create(connector, cornerRadius, ConnectorLineWidth, color);
        _ConnectorPool.Add(path);
        Connectors.Children.Add(path);
      }
    }

    for (var i = _ConnectorPool.Count - 1; i >= connectors.Count; i--)
    {
      RemoveConnector(_ConnectorPool[i]);
      _ConnectorPool.RemoveAt(i);
    }
  }

  // Node views are keyed by person id and kept alive across loads. A cached view is reused as long as
  // its size (zoom) and centre styling still match; mismatches force a single rebuild of that one view.
  private void UpdateNodes(IReadOnlyList<FamilyTreeNodeLayout> nodes, int centerId, IReadOnlyDictionary<int, string> names, double zoom)
  {
    var used = new HashSet<int>();
    foreach (var nodeLayout in nodes)
    {
      var person = nodeLayout.Node.Person;
      used.Add(person.Id);
      var isCenter = person.Id == centerId;
      if (!_NodeCache.TryGetValue(person.Id, out var entry) || entry.Zoom != zoom || entry.IsCenter != isCenter)
      {
        if (entry is not null)
        {
          RemoveNode(entry.View);
        }
        var view = CreateNode(nodeLayout, names[person.Id], isCenter, zoom);
        entry = new NodeEntry(view, zoom, isCenter);
        _NodeCache[person.Id] = entry;
      }
      AbsoluteLayout.SetLayoutBounds(entry.View, nodeLayout.Bounds);
    }

    foreach (var id in _NodeCache.Keys.Where(id => !used.Contains(id)).ToList())
    {
      RemoveNode(_NodeCache[id].View);
      _NodeCache.Remove(id);
    }
  }

  private FamilyTreeNodeView CreateNode(FamilyTreeNodeLayout nodeLayout, string displayName, bool isCenter, double zoom)
  {
    var person = nodeLayout.Node.Person;
    var photo = ResolvePhoto(person);
    var view = new FamilyTreeNodeView(
      photo, _FontScale, displayName, isCenter, nodeLayout.Bounds.Width, nodeLayout.Bounds.Height, zoom);
    view.GestureRecognizers.Add(new TapGestureRecognizer { Command = PageCommand, CommandParameter = person });
    AbsoluteLayout.SetLayoutFlags(view, AbsoluteLayoutFlags.None);
    Nodes.Children.Add(view);
    return view;
  }

  private ImageSource ResolvePhoto(PersonInfo person) =>
    _ThumbnailCache.TryGetValue(person.Id, out var thumbnail) && thumbnail.Length > 0
      ? ImageUtils.ImageFromBytes(thumbnail)
      : ImageUtils.ImageFromRawResource(DefaultPhotoResource(person.BiologicalSex));

  // A node only ever shows a ~60px circle, so keep a small thumbnail per person rather than decoding the
  // full-resolution source for every node (which, now that node views are retained, would pin gigabytes).
  // Decoded here off the UI thread, before Render runs.
  private void CacheThumbnails(FamilyTree tree)
  {
    foreach (var node in tree.Nodes)
    {
      var person = node.Person;
      if (person.MainPhoto is { Content.Length: > 0 } photo)
      {
        _ThumbnailCache.GetOrAdd(person.Id, _ => Downsize(photo.Content));
      }
    }
  }

  private static byte[] Downsize(byte[] content)
  {
    try
    {
      return ImageUtils.DownsizedPng(content, PhotoThumbnailSize);
    }
    catch
    {
      // A photo that cannot be decoded falls back to the stub rather than failing the whole load.
      return [];
    }
  }

  private static string DefaultPhotoResource(BiologicalSex sex) => sex switch
  {
    BiologicalSex.Male => "male_stub.png",
    BiologicalSex.Female => "female_stub.png",
    _ => "project_icon.png",
  };

  private void RemoveNode(FamilyTreeNodeView view)
  {
    Nodes.Children.Remove(view);
    view.Handler?.DisconnectHandler();
  }

  private void RemoveConnector(Path path)
  {
    Connectors.Children.Remove(path);
    path.Handler?.DisconnectHandler();
  }

  // Drop every reused view, connector and thumbnail so the next load rebuilds from current data. Refresh
  // uses this: the page never auto-reloads, so the cached visuals would otherwise keep showing a person's
  // name and photo as they were when first drawn, ignoring edits made since.
  private void ClearRenderCache()
  {
    foreach (var entry in _NodeCache.Values)
    {
      RemoveNode(entry.View);
    }
    foreach (var path in _ConnectorPool)
    {
      RemoveConnector(path);
    }
    _NodeCache.Clear();
    _ConnectorPool.Clear();
    _ThumbnailCache.Clear();
  }

  private async Task PositionViewportAsync(Point centerTopLeft, double zoom)
  {
    // Let the ScrollView measure its new content before scrolling so the viewport size and extents
    // are known.
    await Task.Yield();

    var maxX = Math.Max(0, Canvas.Width - Scroller.Width);
    var maxY = Math.Max(0, Canvas.Height - Scroller.Height);

    // Always keep the centre's column horizontally centred.
    var targetX = centerTopLeft.X + (_Metrics.NodeWidth * zoom / 2) - (Scroller.Width / 2);

    // Vertically, park where the freshly loaded generation appears: the top after loading ancestors,
    // the bottom after loading descendants, otherwise centred on the focal person.
    var targetY = _ViewTarget switch
    {
      ViewTarget.Top => 0,
      ViewTarget.Bottom => maxY,
      _ => centerTopLeft.Y + (_Metrics.NodeHeight * zoom / 2) - (Scroller.Height / 2),
    };

    await Scroller.ScrollToAsync(Math.Clamp(targetX, 0, maxX), Math.Clamp(targetY, 0, maxY), animated: false);
  }

  protected async Task OnPageCommand(object parameter)
  {
    switch (parameter)
    {
      case PersonInfo person when person.Id == _Center?.Id:
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = person });
        break;

      case PersonInfo person:
        SetCenter(person);
        break;

      case string command when command == "OpenPerson" && _Center is PersonInfo center:
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = center });
        break;

      case string command when command == "LoadAncestors":
        var newAncestorGenerations = Math.Min(_AncestorGenerations + GenerationsPerLoad, MaxGenerations);
        if (_AncestorGenerations < newAncestorGenerations)
        {
          _AncestorGenerations = newAncestorGenerations;
          Reload(ViewTarget.Top);
        }
        break;

      case string command when command == "LoadDescendants":
        var newDescendantGenerations = Math.Min(_DescendantGenerations + GenerationsPerLoad, MaxGenerations);
        if (_DescendantGenerations < newDescendantGenerations)
        {
          _DescendantGenerations = newDescendantGenerations;
          Reload(ViewTarget.Bottom);
        }
        break;

      case string command when command == "ZoomIn":
        _ZoomScale = Math.Clamp(_ZoomScale + ZoomStep, MinZoom, MaxZoom);
        Reload(ViewTarget.Center);
        break;

      case string command when command == "ZoomOut":
        _ZoomScale = Math.Clamp(_ZoomScale - ZoomStep, MinZoom, MaxZoom);
        Reload(ViewTarget.Center);
        break;

      case string command when command == "Refresh":
        ClearRenderCache();
        Reload(ViewTarget.Center);
        break;

#if DEBUG
      // Diagnostic: render a deep tree in ONE pass (no incremental Clear()+rebuild churn) to
      // separate a size/element-count limit from per-render rebuild cost. Each tap jumps deeper.
      case string command when command == "LoadDeep":
        _AncestorGenerations = _AncestorGenerations < 64 ? 64 : Math.Min(_AncestorGenerations + 16, MaxGenerations);
        Reload(ViewTarget.Center);
        break;

      // Diagnostic: the leaking path — one fully-awaited incremental render per generation. Used to
      // verify the handler-disconnect fix keeps privateMB flat near the single-render baseline.
      case string command when command == "AutoLoad":
        await AutoLoadAncestorsAsync();
        break;
#endif
    }
  }

#if DEBUG
  private async Task AutoLoadAncestorsAsync()
  {
    if (_Center is null)
    {
      return;
    }

    _ViewTarget = ViewTarget.Top;
    while (_CanLoadMoreAncestors && _AncestorGenerations < MaxGenerations)
    {
      _AncestorGenerations++;
      await LoadAsync(_Center);
      await Task.Delay(400);
    }
  }
#endif

  private static Color GetColor(string resourceKey, Color fallback) =>
    Application.Current?.Resources is { } resources
    && resources.TryGetValue(resourceKey, out var value)
    && value is Color color
      ? color
      : fallback;
}
