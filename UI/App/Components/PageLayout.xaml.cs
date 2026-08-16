using GT4.UI.Items;
using GT4.UI.Utils.Settings;
using System.Collections.ObjectModel;

namespace GT4.UI.Components;

public partial class PageLayout : ContentView
{
  private readonly ObservableCollection<PageMenuItem> _MenuItems = new();
  private bool _IsTopMenuVisible;
  private bool _IsSideMenuVisible;


  public PageLayout(IServiceProvider serviceProvider)
  {
    Animation = serviceProvider.GetRequiredService<BackgroundAnimation>();
    SizeChanged += OnMenuPlacementChanged;
    _MenuItems.CollectionChanged += OnMenuPlacementChanged;

    InitializeComponent();
  }

  public PageLayout()
    : this(GT4Services.Provider)
  {
  }

  public BackgroundAnimation Animation { get; }

  public static readonly BindableProperty TitleProperty =
    BindableProperty.Create(
      nameof(Title),
      typeof(string),
      typeof(PageLayout),
      string.Empty,
      BindingMode.OneWay,
      null,
      OnTitleChanged);

  public static readonly BindableProperty HintProperty =
    BindableProperty.Create(
      nameof(Hint),
      typeof(string),
      typeof(PageLayout),
      string.Empty,
      BindingMode.OneWay,
      null,
      OnHintChanged);

  public static readonly BindableProperty HeaderProperty =
    BindableProperty.Create(
      nameof(Header),
      typeof(View),
      typeof(PageLayout),
      default(View),
      BindingMode.OneWay,
      null,
      OnHeaderChanged);

  public static readonly BindableProperty BodyProperty =
    BindableProperty.Create(
      nameof(Body),
      typeof(View),
      typeof(PageLayout),
      default(View));

  public static readonly BindableProperty FooterProperty =
    BindableProperty.Create(
      nameof(Footer),
      typeof(View),
      typeof(PageLayout),
      default(View),
      BindingMode.OneWay,
      null,
      OnFooterChanged);

  private static void OnHeaderChanged(BindableObject bindableObject, object oldValue, object newValue)
  {
    if (bindableObject is PageLayout view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(IsHeaderVisible));
    }
  }

  private static void OnFooterChanged(BindableObject bindableObject, object oldValue, object newValue)
  {
    if (bindableObject is PageLayout view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(IsFooterVisible));
    }
  }

  private static void OnTitleChanged(BindableObject bindableObject, object oldValue, object newValue)
  {
    if (bindableObject is PageLayout view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(IsTitleVisible));
    }
  }

  private static void OnHintChanged(BindableObject bindableObject, object oldValue, object newValue)
  {
    if (bindableObject is PageLayout view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(IsHintVisible));
    }
  }

  private void OnMenuPlacementChanged(object? sender, EventArgs e)
  {
    var isTopMenuVisible = IsTopMenuVisible;
    var isSideMenuVisible = IsSideMenuVisible;

    if (isTopMenuVisible != _IsTopMenuVisible)
    {
      _IsTopMenuVisible = isTopMenuVisible;
      OnPropertyChanged(nameof(IsTopMenuVisible));
    }

    if (isSideMenuVisible != _IsSideMenuVisible)
    {
      _IsSideMenuVisible = isSideMenuVisible;
      OnPropertyChanged(nameof(IsSideMenuVisible));
    }
  }

  public ICollection<PageMenuItem> MenuItems => _MenuItems;

  public string Title
  {
    get => (string)GetValue(TitleProperty);
    set => SetValue(TitleProperty, value);
  }

  public string Hint
  {
    get => (string)GetValue(HintProperty);
    set => SetValue(HintProperty, value);
  }

  public View Header
  {
    get => (View)GetValue(HeaderProperty);
    set => SetValue(HeaderProperty, value);
  }

  public View Body
  {
    get => (View)GetValue(BodyProperty);
    set => SetValue(BodyProperty, value);
  }

  public View Footer
  {
    get => (View)GetValue(FooterProperty);
    set => SetValue(FooterProperty, value);
  }

  public bool IsMenuVisible => _MenuItems.Count > 0;

  public bool IsTopMenuVisible => IsMenuVisible && Height >= 0 && Height > Width;

  public bool IsSideMenuVisible => IsMenuVisible && Height >= 0 && Height <= Width;

  public bool IsTitleVisible => !string.IsNullOrWhiteSpace(Title);

  public bool IsHintVisible => !string.IsNullOrWhiteSpace(Hint);

  public bool IsHeaderVisible => Header is not null;

  public bool IsFooterVisible => Footer is not null;
}
