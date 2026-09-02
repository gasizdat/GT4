using System.ComponentModel;
using System.Windows.Input;

namespace GT4.UI.Components;

/// <summary>
/// One row of the flattened relatives tree: the connector lines, the level indentation and the "more"
/// button wrapped around a leaf <see cref="RelativeInfoView"/>. Bound to a <see cref="RelativeRow"/>;
/// everything is derived from that row so a recycled view never shows another node's lines or indent.
/// </summary>
public partial class RelativeRowView : ContentView
{
  private static readonly double Indent = DeviceInfo.Idiom == DeviceIdiom.Phone ? 10 : 20;
  private static readonly double RowSpacing = DeviceInfo.Idiom == DeviceIdiom.Phone ? 5 : 10;

  private readonly RelativeConnectorsDrawable _Connectors = new() { Indent = Indent };
  private RelativeRow? _BoundRow;

  protected RelativeConnectorsDrawable ConnectorsDrawable => _Connectors;

  protected RelativeInfoView InfoView => Info;

  protected Thickness ContentMargin => ContentRoot.Margin;

  public RelativeRowView()
  {
    InitializeComponent();
    Connectors.Drawable = _Connectors;
    Info.PropertyChanged += OnInfoPropertyChanged;
    ContentRoot.SizeChanged += OnContentSizeChanged;
  }

  // A GraphicsView left to Fill inside a virtualized item measures to zero and loses the trunk lines,
  // so it needs an explicit size -- taken from the content and never from LayoutRoot, which is one
  // cell as tall as the taller of the two and so would feed the overlay its own height back.
  private void OnContentSizeChanged(object? sender, EventArgs e)
  {
    Connectors.WidthRequest = ContentRoot.Margin.Left + ContentRoot.Width;
    Connectors.HeightRequest = ContentRoot.Height;
    Connectors.Invalidate();
  }

  public static readonly BindableProperty SelectCommandProperty = BindableProperty.Create(
    nameof(SelectCommand),
    typeof(ICommand),
    typeof(RelativeRowView),
    default,
    BindingMode.OneWay);

  public ICommand? SelectCommand
  {
    get => (ICommand?)GetValue(SelectCommandProperty);
    set => SetValue(SelectCommandProperty, value);
  }

  protected override void OnBindingContextChanged()
  {
    base.OnBindingContextChanged();

    if (_BoundRow is not null)
    {
      _BoundRow.PropertyChanged -= OnRowPropertyChanged;
    }

    var row = BindingContext as RelativeRow;
    _BoundRow = row;
    if (row is not null)
    {
      row.PropertyChanged += OnRowPropertyChanged;
    }

    _Connectors.Row = row;
    UpdateIndentMargin(row);
    _Connectors.PhotoCenterY = Info.PersonInfoFrame.Center.Y;
    Connectors.Invalidate();
  }

  // A recycled view can stay bound to the same row across a filter change (FilteredObservableCollection
  // only raises events for rows whose own visibility changed), so react to IsFilterActive explicitly.
  private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(RelativeRow.IsFilterActive))
    {
      UpdateIndentMargin(_BoundRow);
      Connectors.Invalidate();
    }
  }

  private void UpdateIndentMargin(RelativeRow? row)
  {
    var depth = row is null || row.IsFilterActive ? 0 : row.Depth;
    ContentRoot.Margin = new Thickness(left: depth * Indent, top: 0, right: 0, bottom: RowSpacing);
  }

  private void OnInfoPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(RelativeInfoView.PersonInfoFrame))
    {
      _Connectors.PhotoCenterY = Info.PersonInfoFrame.Center.Y;
      Connectors.Invalidate();
    }
  }
}
