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
  private Person? _Center;
  private string _CenterName = string.Empty;
  private int _AncestorGenerations = 3;
  private int _DescendantGenerations = 3;
  private bool _IncludeCollaterals = false;

  public FamilyTreePage(IServiceProvider serviceProvider)
  {
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _NameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    PageCommand = new SafeCommand(OnPageCommand);

    InitializeComponent();

    _ConnectorsDrawable.CornerRadius = _Metrics.CornerRadius;
    _ConnectorsDrawable.ParentChildColor = GetColor("Primary", Color.FromArgb("#1E4437"));
    _ConnectorsDrawable.SpouseColor = GetColor("Accent", Color.FromArgb("#8B6F4E"));
    Connectors.Drawable = _ConnectorsDrawable;
  }

  public ICommand PageCommand { get; }

  public PersonInfo PersonInfo
  {
    set => SetCenter(value);
  }

  public string PageTitle => string.Format(UIStrings.TitleFamilyTreePage_1, _CenterName);

  public string OpenPersonToolbarItemName => string.Format(UIStrings.MenuItemNameOpenPerson_1, _CenterName);

  public int AncestorGenerations
  {
    get => _AncestorGenerations;
    set
    {
      if (_AncestorGenerations == value)
      {
        return;
      }

      _AncestorGenerations = value;
      OnPropertyChanged(nameof(AncestorGenerations));
      Reload();
    }
  }

  public int DescendantGenerations
  {
    get => _DescendantGenerations;
    set
    {
      if (_DescendantGenerations == value)
      {
        return;
      }

      _DescendantGenerations = value;
      OnPropertyChanged(nameof(DescendantGenerations));
      Reload();
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
      Reload();
    }
  }

  private void SetCenter(PersonInfo person)
  {
    _Center = person;
    _CenterName = _NameFormatter.ToString(person, NameFormat.ShortPersonName);
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

    Canvas.WidthRequest = layout.CanvasSize.Width;
    Canvas.HeightRequest = layout.CanvasSize.Height;
    Connectors.WidthRequest = layout.CanvasSize.Width;
    Connectors.HeightRequest = layout.CanvasSize.Height;
    Connectors.Invalidate();

    _ = CenterViewportAsync(layout.CenterTopLeft);
  }

  private async Task CenterViewportAsync(Point centerTopLeft)
  {
    // Let the ScrollView measure its content before scrolling so the viewport size is known.
    await Task.Yield();

    var targetX = centerTopLeft.X + (_Metrics.NodeWidth / 2) - (Scroller.Width / 2);
    var targetY = centerTopLeft.Y + (_Metrics.NodeHeight / 2) - (Scroller.Height / 2);

    await Scroller.ScrollToAsync(Math.Max(0, targetX), Math.Max(0, targetY), animated: false);
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

      case string command when command == "Refresh":
        Reload();
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
