using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components.Genealogy;
using GT4.UI.Resources;
using GT4.UI.Utils.Formatters;
using Microsoft.Maui.Layouts;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(PersonInfo), "PersonInfo")]
public partial class FamilyTreePage : ContentPage
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly INameFormatter _NameFormatter;
  private readonly FamilyTreeLayoutMetrics _Metrics = new();
  private readonly FamilyTreeConnectorsDrawable _ConnectorsDrawable = new();
  // Each "load more" click adds one generation, up to this hard ceiling.
  private const int MaxGenerations = 12;
  private const int InitialGenerations = 2;

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

  // Where to park the viewport after a (re)build.
  private enum ViewTarget { Center, Top, Bottom }

  public FamilyTreePage(
    ICancellationTokenProvider cancellationTokenProvider, 
    ICurrentProjectProvider currentProjectProvider,
    INameFormatter nameFormatter
  )
  {
    _CancellationTokenProvider = cancellationTokenProvider;
    _CurrentProjectProvider = currentProjectProvider;
    _NameFormatter = nameFormatter;
    PageCommand = new SafeCommand(OnPageCommand);

    InitializeComponent();

    _ConnectorsDrawable.CornerRadius = _Metrics.CornerRadius;
    _ConnectorsDrawable.ParentChildColor = GetColor("Primary", Color.FromArgb("#1E4437"));
    _ConnectorsDrawable.SpouseColor = GetColor("Accent", Color.FromArgb("#8B6F4E"));
    Connectors.Drawable = _ConnectorsDrawable;

    // Drag-to-pan: the ScrollView already handles wheel, scrollbars and touch flicks, but desktop
    // users expect to grab the canvas and drag it. Translate the pan delta into a scroll offset.
    var pan = new PanGestureRecognizer();
    pan.PanUpdated += OnCanvasPan;
    Canvas.GestureRecognizers.Add(pan);
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
    get => _CanLoadMoreAncestors;
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
    get => _CanLoadMoreDescendants;
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

  private void SetCenter(PersonInfo person)
  {
    _Center = person;
    _CenterName = _NameFormatter.ToString(person, NameFormat.ShortPersonName);
    // A new centre starts a fresh view, so reset the depth back to the default.
    _AncestorGenerations = InitialGenerations;
    _DescendantGenerations = InitialGenerations;
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
    _ = SafeTask.Run(() => LoadAsync(center));
  }

  private async Task LoadAsync(Person center)
  {
    using var token = _CancellationTokenProvider.CreateDbCancellationToken();
    var tree = await _CurrentProjectProvider
      .Project
      .FamilyTreeProvider
      .BuildAsync(center, _AncestorGenerations, _DescendantGenerations, _IncludeCollaterals, token);

    var layout = FamilyTreeLayout.Build(tree, _Metrics);
    var names = layout.Nodes.ToDictionary(
      node => node.Node.Id,
      node => _NameFormatter.ToString(node.Node.Person, NameFormat.ShortPersonName));

    await SafeTask.RunOnMainThread(() => Render(tree.CenterId, layout, names));
  }

  private void Render(int centerId, FamilyTreeLayoutResult layout, IReadOnlyDictionary<int, string> names)
  {
    Nodes.Children.Clear();

    foreach (var nodeLayout in layout.Nodes)
    {
      var person = nodeLayout.Node.Person;
      var isCenter = person.Id == centerId;
      var view = new FamilyTreeNodeView(
        person,
        names[person.Id],
        isCenter,
        _Metrics.NodeWidth,
        _Metrics.NodeHeight);

      view.GestureRecognizers.Add(new TapGestureRecognizer
      {
        Command = PageCommand,
        CommandParameter = person,
      });

      AbsoluteLayout.SetLayoutFlags(view, AbsoluteLayoutFlags.None);
      AbsoluteLayout.SetLayoutBounds(view, nodeLayout.Bounds);
      Nodes.Children.Add(view);
    }

    _ConnectorsDrawable.Connectors = layout.Connectors;

    // A "load more" button is offered only while the tree actually reaches the requested depth: if a
    // generation came back shorter than asked for, that direction has no more data to fetch.
    var maxGeneration = layout.Nodes.Count == 0 ? 0 : layout.Nodes.Max(node => node.Node.Generation);
    var minGeneration = layout.Nodes.Count == 0 ? 0 : layout.Nodes.Min(node => node.Node.Generation);
    CanLoadMoreAncestors = _AncestorGenerations < MaxGenerations && maxGeneration >= _AncestorGenerations;
    CanLoadMoreDescendants = _DescendantGenerations < MaxGenerations && minGeneration <= -_DescendantGenerations;

    Canvas.WidthRequest = layout.CanvasSize.Width;
    Canvas.HeightRequest = layout.CanvasSize.Height;
    Connectors.WidthRequest = layout.CanvasSize.Width;
    Connectors.HeightRequest = layout.CanvasSize.Height;
    Connectors.Invalidate();

    _ = PositionViewportAsync(layout.CenterTopLeft);
  }

  private async Task PositionViewportAsync(Point centerTopLeft)
  {
    // Let the ScrollView measure its new content before scrolling so the viewport size and extents
    // are known.
    await Task.Yield();

    var maxX = Math.Max(0, Canvas.Width - Scroller.Width);
    var maxY = Math.Max(0, Canvas.Height - Scroller.Height);

    // Always keep the centre's column horizontally centred.
    var targetX = centerTopLeft.X + (_Metrics.NodeWidth / 2) - (Scroller.Width / 2);

    // Vertically, park where the freshly loaded generation appears: the top after loading ancestors,
    // the bottom after loading descendants, otherwise centred on the focal person.
    var targetY = _ViewTarget switch
    {
      ViewTarget.Top => 0,
      ViewTarget.Bottom => maxY,
      _ => centerTopLeft.Y + (_Metrics.NodeHeight / 2) - (Scroller.Height / 2),
    };

    await Scroller.ScrollToAsync(Math.Clamp(targetX, 0, maxX), Math.Clamp(targetY, 0, maxY), animated: false);
  }

  private async Task OnPageCommand(object parameter)
  {
    switch (parameter)
    {
      case PersonInfo person when person.Id == _Center?.Id:
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = person });
        break;

      case PersonInfo person:
        SetCenter(person);
        break;

      case string command when command == "OpenPerson" && _Center is PersonInfo center:
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { ["PersonInfo"] = center });
        break;

      case string command when command == "LoadAncestors":
        if (_AncestorGenerations < MaxGenerations)
        {
          _AncestorGenerations++;
          Reload(ViewTarget.Top);
        }
        break;

      case string command when command == "LoadDescendants":
        if (_DescendantGenerations < MaxGenerations)
        {
          _DescendantGenerations++;
          Reload(ViewTarget.Bottom);
        }
        break;

      case string command when command == "Refresh":
        Reload(ViewTarget.Center);
        break;
    }
  }

  private static Color GetColor(string resourceKey, Color fallback) =>
    Application.Current?.Resources is { } resources
    && resources.TryGetValue(resourceKey, out var value)
    && value is Color color
      ? color
      : fallback;
}
