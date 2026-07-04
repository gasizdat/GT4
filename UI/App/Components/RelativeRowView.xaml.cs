using System.Windows.Input;

namespace GT4.UI.Components;

/// <summary>
/// One row of the flattened relatives tree: the connector lines, the level indentation and the "more"
/// button wrapped around a leaf <see cref="RelativeInfoView"/>. Bound to a <see cref="RelativeRow"/>;
/// everything is derived from that row so a recycled view never shows another node's lines or indent.
/// </summary>
public partial class RelativeRowView : ContentView
{
  // Mirrors the former RelativesIndentation / PageContentSpacing OnIdiom resources.
  private static readonly double Indent = DeviceInfo.Idiom == DeviceIdiom.Phone ? 10 : 20;
  private static readonly double RowSpacing = DeviceInfo.Idiom == DeviceIdiom.Phone ? 5 : 10;

  private readonly RelativeConnectorsDrawable _Connectors = new() { Indent = Indent };

  protected RelativeConnectorsDrawable ConnectorsDrawable => _Connectors;

  protected RelativeInfoView InfoView => Info;

  public RelativeRowView()
  {
    InitializeComponent();
    Connectors.Drawable = _Connectors;
    Info.PropertyChanged += OnInfoPropertyChanged;
    LayoutRoot.SizeChanged += OnLayoutSizeChanged;
  }

  // A GraphicsView relying on Fill inside a virtualized item often measures to zero height, which would
  // drop the full-height trunk lines. Size it explicitly from the measured row instead (the same reason
  // FamilyTreePage's connector GraphicsView is given an explicit size).
  private void OnLayoutSizeChanged(object? sender, EventArgs e)
  {
    Connectors.WidthRequest = LayoutRoot.Width;
    Connectors.HeightRequest = LayoutRoot.Height;
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
    var row = BindingContext as RelativeRow;
    _Connectors.Row = row;
    ContentRoot.Margin = new Thickness(left: (row?.Depth ?? 0) * Indent, top: 0, right: 0, bottom: RowSpacing);
    _Connectors.PhotoCenterY = Info.PersonInfoFrame.Center.Y;
    Connectors.Invalidate();
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
