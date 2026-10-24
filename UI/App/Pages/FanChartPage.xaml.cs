using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Abstraction;
using GT4.UI.Components.Genealogy;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Dto;
using GT4.UI.Utils.Formatters;
using GT4.UI.Utils.Genealogy;
using GT4.UI.Utils.Settings;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class FanChartPage : ContentPage, IZoomablePage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly IFamilyTreeHiddenPersonsStore _HiddenPersonsStore;
  private readonly INameFormatter _NameFormatter;
  private readonly ILifeDatesFormatter _LifeDatesFormatter;
  private readonly ISettingEditor _SweepSetting;
  private readonly SettingKind.Option[] _SweepOptions;
  private readonly DisplayCalendar _Calendar;
  private readonly FontScale? _FontScale;
  private readonly IAlertService _AlertService;
  private readonly INavigationService _NavigationService;
  private readonly DataConverterResolver _DataConverterResolver;
  private readonly FanChartMetrics _Metrics = new() { Margin = OverlayClearance };
  private readonly FamilyTreeLayoutMetrics _CenterMetrics = new();
  private readonly PageZoom _Zoom = new();
  private const double LineWidth = 1.5;
  private const double HighlightedLineWidth = 2 * LineWidth;
  private const double FontSizeBase = 12;
  private const int InitialGenerations = 4;
  private const int GenerationsPerLoad = 2;
  // Past this, a ring's segments are too thin to show or hit at any zoom.
  private const int MaxGenerations = 10;
  // Sized to clear the tallest of the "load more" and zoom buttons pinned over the canvas.
  private const double OverlayClearance = 72;

  private PersonInfo? _Center;
  private string _CenterName = string.Empty;
  private int _Generations = InitialGenerations;
  private bool _CanLoadMore;
  private bool _HasFallbackYears;
  private double _PanStartScrollX;
  private double _PanStartScrollY;
  private int _LoadOperationsCount = 0;
  private int _LatestLoad;
  private ProjectInfo? _LastProjectInfo;
  private int[] _HiddenIds = [];
  private FanChartLayoutResult? _LastLayout;
  private Color? _Stroke;
  private FamilyTreeNodeView? _CenterView;
  private PersonInfo? _HoveredPerson;

  public FanChartPage(
    ICancellationTokenProvider cancellationTokenProvider,
    ICurrentProjectProvider currentProjectProvider,
    IFamilyTreeHiddenPersonsStore hiddenPersonsStore,
    INameFormatter nameFormatter,
    IDateFormatter dateFormatter,
    ILifeDatesFormatter lifeDatesFormatter,
    [FromKeyedServices(SettingKeys.FanChartSweep)]
    ISettingEditor sweepSetting,
    [FromKeyedServices(SettingKeys.Calendar)]
    ISettingEditor calendarSetting,
    FontScale? fontScale,
    IAlertService alertService,
    INavigationService navigationService,
    DataConverterResolver dataConverterResolver
  )
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _HiddenPersonsStore = hiddenPersonsStore;
    _NameFormatter = nameFormatter;
    _LifeDatesFormatter = lifeDatesFormatter;
    _SweepSetting = sweepSetting;
    _SweepOptions = ((SettingKind.Choice)sweepSetting.Kind).Options;
    _Calendar = CalendarConversion.ToDisplayCalendar(calendarSetting.Value);
    CalendarName = dateFormatter.CalendarName(_Calendar);
    _FontScale = fontScale;
    _AlertService = alertService;
    _NavigationService = navigationService;
    _DataConverterResolver = dataConverterResolver;
    Loading = new PageLoading(_AlertService);
    PageCommand = new SafeCommand(OnPageCommand, _AlertService);

    InitializeComponent();

    // Every layer is input-transparent, so the canvas takes every gesture: a sector's bounding rectangle
    // overlaps its neighbours', and only the angle and radius tell them apart.
    var pan = new PanGestureRecognizer();
    pan.PanUpdated += OnCanvasPan;
    Canvas.GestureRecognizers.Add(pan);
    var tap = new TapGestureRecognizer();
    tap.Tapped += OnCanvasTapped;
    Canvas.GestureRecognizers.Add(tap);
    var pointer = new PointerGestureRecognizer();
    pointer.PointerMoved += OnCanvasPointerMoved;
    pointer.PointerExited += OnCanvasPointerExited;
    Canvas.GestureRecognizers.Add(pointer);
#if ANDROID
    // Touch has no hover, so a held finger stands in for the pointer until it lifts.
    pointer.PointerReleased += OnCanvasPointerExited;
    var hold = TouchHold.Create(OnCanvasHeld);
    Canvas.GestureRecognizers.Add(hold);
#endif
    // A new size reaches the scroll extents only once the canvas has been laid out at it; centring any
    // earlier clamps the scroll to the old extents.
    Canvas.SizeChanged += (_, _) => _ = CenterViewportAsync();
    _LastProjectInfo = _CurrentProjectProvider.Info;
  }

  public PageLoading Loading { get; }

  public ICommand PageCommand { get; }

  public PersonInfo PersonInfo
  {
    set => SetCenter(value);
  }

  public string PageTitle => string.Format(UIStrings.TitleFanChartPage_1, _CenterName);

  public string OpenPersonToolbarItemName => string.Format(UIStrings.MenuItemNameOpenPerson_1, _CenterName);

  public string[] SweepNames => [.. _SweepOptions.Select(option => option.Label)];

  public int SweepIndex
  {
    get => Array.FindIndex(_SweepOptions, option => option.Value == _SweepSetting.Value);
    set
    {
      if (SweepIndex == value)
      {
        return;
      }

      _SweepSetting.Value = _SweepOptions[value].Value;
      OnPropertyChanged(nameof(SweepIndex));
      Reload();
    }
  }

  public bool IsCalendarVisible => _Calendar != DisplayCalendar.Gregorian;

  public string CalendarName { get; }

  // A no-break space, so a wrapped mark keeps the dot on the legend's line.
  public string FallbackLegend => _HasFallbackYears ? $" ·\u00A0{UIStrings.FieldCalendarGregorian}" : string.Empty;

  public bool CanLoadMore
  {
    get => _CanLoadMore && !LoadInProgress;
    private set
    {
      if (_CanLoadMore == value)
      {
        return;
      }

      _CanLoadMore = value;
      OnPropertyChanged(nameof(CanLoadMore));
    }
  }

  public bool LoadInProgress
  {
    get => _LoadOperationsCount != 0;
  }

  public PersonInfo? HoveredPerson
  {
    get => _HoveredPerson;
    private set
    {
      _HoveredPerson = value;
      OnPropertyChanged(nameof(HoveredPerson));
    }
  }

  public void Zoom(double delta)
  {
    if (_Zoom.Pinch(delta))
    {
      Reload();
    }
  }

  public void ResetZoom()
  {
    if (_Zoom.Reset())
    {
      Reload();
    }
  }

  protected void TapAt(Point point)
  {
    var node = _LastLayout is null ? null : FanChartLayout.HitTest(_LastLayout, point);
    if (node is not null)
    {
      PageCommand.Execute(node.Person);
    }
  }

  protected void HoverAt(Point? point)
  {
    var node = point is Point at && _LastLayout is not null ? FanChartLayout.HitTest(_LastLayout, at) : null;
    SetHovered(node?.Person);
  }

  // An edit on a person page, or a hide on a family tree, opened from here must show on the way back.
  protected override void OnNavigatedTo(NavigatedToEventArgs args)
  {
    base.OnNavigatedTo(args);
    var hiddenIds = _HiddenPersonsStore.Get(_CurrentProjectProvider.Info);
    if (_LastProjectInfo != _CurrentProjectProvider.Info || !hiddenIds.SequenceEqual(_HiddenIds))
    {
      _LastProjectInfo = _CurrentProjectProvider.Info;
      _HiddenIds = hiddenIds;
      Reload();
    }
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

      case string command when command == "OpenPerson" && _Center is not null:
        await _NavigationService.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = _Center });
        break;

      case string command when command == "LoadMore":
        _Generations = Math.Min(_Generations + GenerationsPerLoad, MaxGenerations);
        Reload();
        break;

      case string command when command == "ZoomIn":
        if (_Zoom.ZoomIn())
        {
          Reload();
        }
        break;

      case string command when command == "ZoomOut":
        if (_Zoom.ZoomOut())
        {
          Reload();
        }
        break;

      case string command when command == "Refresh":
        Reload();
        break;
    }
  }

  private void OnCanvasPan(object? sender, PanUpdatedEventArgs e)
  {
    switch (e.StatusType)
    {
      case GestureStatus.Started:
        _PanStartScrollX = Scroller.ScrollX;
        _PanStartScrollY = Scroller.ScrollY;
        break;

      case GestureStatus.Running:
        var maxX = Math.Max(0, Canvas.Width - Scroller.Width);
        var maxY = Math.Max(0, Canvas.Height - Scroller.Height);
        var targetX = Math.Clamp(_PanStartScrollX - e.TotalX, 0, maxX);
        var targetY = Math.Clamp(_PanStartScrollY - e.TotalY, 0, maxY);
        _ = Scroller.ScrollToAsync(targetX, targetY, animated: false);
        break;
    }
  }

  private void OnCanvasTapped(object? sender, TappedEventArgs e)
  {
    if (e.GetPosition(Canvas) is Point point)
    {
      TapAt(point);
    }
  }

  private void OnCanvasPointerMoved(object? sender, PointerEventArgs e) => HoverAt(e.GetPosition(Canvas));

  private void OnCanvasPointerExited(object? sender, PointerEventArgs e) => HoverAt(null);

#if ANDROID
  private void OnCanvasHeld(DragStartingEventArgs e) => HoverAt(e.GetPosition(Canvas));
#endif

  private void SetHovered(PersonInfo? person)
  {
    var personId = person?.Id;
    if (personId == HoveredPerson?.Id)
    {
      return;
    }

    HoveredPerson = person;
    var layout = _LastLayout!;
    ClearLayer(Highlights);
    // On a layer of their own: raising a segment over its neighbours instead reorders the native
    // children of a layer that can hold thousands of them.
    var outlines = layout
      .Segments
      .Where(segment => segment.Node.Id == personId)
      .Select(segment => FanChartSegmentShape.Create(segment, layout.Origin, HighlightedLineWidth, Colors.Transparent, _Stroke!));
    foreach (var outline in outlines)
    {
      Highlights.Children.Add(outline);
    }

    _CenterView!.SetHighlighted(personId == layout.Center.Id);
  }

  private void SetCenter(PersonInfo person)
  {
    _Center = person;
    _CenterName = _NameFormatter.ToString(person, NameFormat.ShortPersonName);
    _Generations = InitialGenerations;
    _HiddenIds = _HiddenPersonsStore.Get(_CurrentProjectProvider.Info);
    OnPropertyChanged(nameof(PageTitle));
    OnPropertyChanged(nameof(OpenPersonToolbarItemName));
    Reload();
  }

  private void Reload()
  {
    if (_Center is null)
    {
      return;
    }

    var center = _Center;
    var generations = _Generations;
    var hiddenIds = _HiddenIds;
    var load = ++_LatestLoad;
    SetLoadInProgress();
    _ = SafeTask.Run(() => LoadAsync(load, center, generations, hiddenIds), _AlertService);
  }

  private void SetLoadInProgress()
  {
    _LoadOperationsCount++;
    Loading.Begin(hasContent: _LastLayout is not null);

    OnPropertyChanged(nameof(LoadInProgress));
    OnPropertyChanged(nameof(CanLoadMore));
  }

  private void ResetLoadInProgress()
  {
    _LoadOperationsCount = Math.Max(_LoadOperationsCount - 1, 0);
    Loading.End();

    OnPropertyChanged(nameof(LoadInProgress));
    OnPropertyChanged(nameof(CanLoadMore));
  }

  private async Task LoadAsync(int load, Person center, int generations, int[] hiddenIds)
  {
    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var tree = await _CurrentProjectProvider
        .Project
        .FamilyTreeProvider
        .BuildAsync(center, generations, descendantGenerations: 0, includeCollaterals: false, hiddenIds, token);

      var zoom = _Zoom.Scale;
      var fontFactor = _FontScale?.CurrentFactor ?? FontScale.DefaultFactor;
      var metrics = _Metrics with
      {
        CenterRadius = _Metrics.CenterRadius * zoom,
        RingWidth = _Metrics.RingWidth * zoom,
        LineHeight = _Metrics.LineHeight * zoom * fontFactor,
        // Not the zoom: zooming in shrinks the text to the unzoomed size before it gives up a label.
        MinLineHeight = _Metrics.MinLineHeight * fontFactor,
        // Not the zoom: this metric clears fixed-size overlays, so it tracks the font that sizes them.
        Margin = _Metrics.Margin * fontFactor,
      };
      var sweep = FanChartSweep.Parse(_SweepSetting.Value);
      var layout = FanChartLayout.Compute(tree, generations, sweep, metrics);
      var labels = layout
        .Segments
        .Where(segment => segment.Label != FanChartLabel.None)
        .ToDictionary(segment => segment.Slot, LabelRuns);
      var centerName = _NameFormatter.ToString(layout.Center.Person, NameFormat.ShortPersonName);
      var photo = await LoadPhotoAsync(layout.Center.Person, token);

      await SafeTask.RunOnMainThread(() => Render(load, layout, generations, labels, centerName, photo, zoom), _AlertService);
    }
    finally
    {
      await SafeTask.RunOnMainThread(ResetLoadInProgress, _AlertService);
    }
  }

  private (string Text, bool IsFallback)[] LabelRuns(FanChartSegment segment)
  {
    var name = _NameFormatter.ToString(segment.Node.Person, NameFormat.ShortPersonName);
    var years = _LifeDatesFormatter.ToYears(segment.Node.Person, _Calendar);
    return segment.Label == FanChartLabel.Full && years.Length != 0 ? [($"{name}\n", false), .. years] : [(name, false)];
  }

  private async Task<ImageSource> LoadPhotoAsync(PersonInfo person, CancellationToken token)
  {
    if (person.MainPhoto is not null)
    {
      var thumbnail = new ImageDataWithMaxSize(person.MainPhoto, ImageUtils.ThumbnailSize);
      var converter = _DataConverterResolver(person.MainPhoto.Category);
      if (await converter.ToObjectAsync(thumbnail, token) is PhotoInfo photoInfo)
      {
        return photoInfo.Source;
      }
    }

    var resourceName = ImageUtils.DefaultPersonPhotoResourceName(person.BiologicalSex);
    return ImageUtils.ImageFromRawResource(resourceName, ImageUtils.ThumbnailSize);
  }

  private void Render(
    int load,
    FanChartLayoutResult layout,
    int generations,
    IReadOnlyDictionary<int, (string Text, bool IsFallback)[]> labels,
    string centerName,
    ImageSource photo,
    double zoom)
  {
    // Every draw rebuilds the whole chart, and an older load can finish after the latest one.
    if (load != _LatestLoad)
    {
      return;
    }

    ClearCanvas();

    var fill = ThemedColor.Resolve("ControlFill", Color.FromArgb("#1A1E4437"));
    var repeatFill = ThemedColor.Resolve("SurfaceSubtle", Color.FromArgb("#EAE7E0"));
    var fallbackText = ThemedColor.Resolve("MutedText", Color.FromArgb("#6B6659"));
    _Stroke = ThemedColor.Resolve("Outline", Color.FromArgb("#991E4437"));
    var fontSizePerLineHeight = FontSizeBase / _Metrics.LineHeight;
    var origin = layout.Origin;
    var radius = layout.Metrics.CenterRadius;

    var disc = new Ellipse { Fill = fill, Stroke = _Stroke, StrokeThickness = LineWidth };
    var discBounds = new Rect(origin.X - radius, origin.Y - radius, 2 * radius, 2 * radius);
    AbsoluteLayout.SetLayoutBounds(disc, discBounds);
    Segments.Children.Add(disc);

    foreach (var segment in layout.Segments)
    {
      var segmentFill = segment.IsRepeat ? repeatFill : fill;
      var shape = FanChartSegmentShape.Create(segment, origin, LineWidth, segmentFill, _Stroke);
      Segments.Children.Add(shape);

      if (labels.TryGetValue(segment.Slot, out var runs))
      {
        var label = CreateLabel(segment, runs, segment.LineHeight * fontSizePerLineHeight, fallbackText);
        Labels.Children.Add(label);
      }
    }

    _HasFallbackYears = labels.Values.Any(runs => runs.Any(run => run.IsFallback));
    OnPropertyChanged(nameof(FallbackLegend));

    var width = _CenterMetrics.NodeWidth * zoom;
    var height = _CenterMetrics.NodeHeight * zoom;
    var centerView = new FamilyTreeNodeView(photo, _FontScale, centerName, isCenter: true, width, height, zoom);
    var centerBounds = new Rect(origin.X - (width / 2), origin.Y - (height / 2), width, height);
    AbsoluteLayout.SetLayoutBounds(centerView, centerBounds);
    Labels.Children.Add(centerView);
    _CenterView = centerView;

    // Offered only while the chart reaches the requested depth: a shorter one has no more data to fetch.
    CanLoadMore = generations < MaxGenerations && layout.OuterGeneration >= generations;

    Canvas.WidthRequest = layout.CanvasSize.Width;
    Canvas.HeightRequest = layout.CanvasSize.Height;
    _LastLayout = layout;

    _ = CenterViewportAsync();
  }

  private static Label CreateLabel(FanChartSegment segment, (string Text, bool IsFallback)[] runs, double fontSize, Color fallbackText)
  {
    var size = segment.LabelSize;
    var center = segment.LabelCenter;
    var text = new FormattedString();
    foreach (var run in runs)
    {
      var span = new Span { Text = run.Text, TextColor = run.IsFallback ? fallbackText : null };
      text.Spans.Add(span);
    }

    var label = new Label
    {
      FormattedText = text,
      FontSize = fontSize,
      MaxLines = segment.Label == FanChartLabel.Full ? 2 : 1,
      LineBreakMode = LineBreakMode.TailTruncation,
      HorizontalTextAlignment = TextAlignment.Center,
      VerticalTextAlignment = TextAlignment.Center,
      Rotation = segment.LabelRotation,
    };
    var bounds = new Rect(center.X - (size.Width / 2), center.Y - (size.Height / 2), size.Width, size.Height);
    AbsoluteLayout.SetLayoutFlags(label, AbsoluteLayoutFlags.None);
    AbsoluteLayout.SetLayoutBounds(label, bounds);
    return label;
  }

  private void ClearCanvas()
  {
    // Forgotten with the outlines, or the next move over the same person would leave the new chart unlit.
    HoveredPerson = null;
    ClearLayer(Segments);
    ClearLayer(Highlights);
    ClearLayer(Labels);
  }

  // Disconnected to release each removed view's native resources.
  private static void ClearLayer(AbsoluteLayout layer)
  {
    View[] views = [.. layer.Children.OfType<View>()];
    layer.Children.Clear();
    foreach (var view in views)
    {
      view.Handler?.DisconnectHandler();
    }
  }

  private async Task CenterViewportAsync()
  {
    // Let the ScrollView measure its new content first, so the viewport size and extents are known.
    await Task.Yield();

    if (_LastLayout is null)
    {
      return;
    }

    var origin = _LastLayout.Origin;
    var maxX = Math.Max(0, Canvas.Width - Scroller.Width);
    var maxY = Math.Max(0, Canvas.Height - Scroller.Height);
    var targetX = Math.Clamp(origin.X - (Scroller.Width / 2), 0, maxX);
    var targetY = Math.Clamp(origin.Y - (Scroller.Height / 2), 0, maxY);
    await Scroller.ScrollToAsync(targetX, targetY, animated: false);
  }
}
