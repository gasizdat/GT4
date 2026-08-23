using GT4.UI.Abstraction;
using GT4.UI.Items;
using GT4.UI.Resources;
using GT4.UI.Utils.Settings;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class PageLayout : ContentView
{
  private readonly ObservableCollection<PageMenuItem> _MenuItems = new();
  private readonly INavigationService _NavigationService;
  private readonly ICommand _GoBackCommand;
  private bool _IsTopMenuVisible;
  private bool _IsSideMenuVisible;


  public PageLayout(IServiceProvider serviceProvider)
  {
    Animation = serviceProvider.GetRequiredService<BackgroundAnimation>();
    _NavigationService = serviceProvider.GetRequiredService<INavigationService>();

    var alertService = serviceProvider.GetRequiredService<IAlertService>();
    _GoBackCommand = new SafeCommand(GoBackAsync, alertService);
    BackItem = new PageMenuItem
    {
      Text = UIStrings.MenuItemNameBack,
      Command = _GoBackCommand,
      CommandParameter = GoBackCommandParameter
    };

    SizeChanged += OnMenuPlacementChanged;
    _MenuItems.CollectionChanged += OnMenuPlacementChanged;

    InitializeComponent();
  }

  public PageLayout()
    : this(GT4Services.Provider)
  {
  }

  // A dialog binding BackCommand to its one generic command tells this case apart by the parameter.
  public const string GoBackCommandParameter = "PageLayout.GoBackCommand";

  public BackgroundAnimation Animation { get; }

  public PageMenuItem BackItem { get; }

  public static readonly BindableProperty HasBackButtonProperty =
    BindableProperty.Create(
      nameof(HasBackButton),
      typeof(bool),
      typeof(PageLayout),
      false,
      BindingMode.OneWay,
      null,
      OnHasBackButtonChanged);

  public static readonly BindableProperty BackCommandProperty =
    BindableProperty.Create(
      nameof(BackCommand),
      typeof(ICommand),
      typeof(PageLayout),
      default(ICommand),
      BindingMode.OneWay,
      null,
      OnBackCommandChanged);

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

  public static readonly BindableProperty LoadingProperty =
    BindableProperty.Create(
      nameof(Loading),
      typeof(PageLoading),
      typeof(PageLayout),
      default(PageLoading));

  public static readonly BindableProperty FooterProperty =
    BindableProperty.Create(
      nameof(Footer),
      typeof(View),
      typeof(PageLayout),
      default(View),
      BindingMode.OneWay,
      null,
      OnFooterChanged);

  private static void OnHasBackButtonChanged(BindableObject bindableObject, object oldValue, object newValue)
  {
    if (bindableObject is PageLayout view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(IsBackButtonVisible));
    }
  }

  private static void OnBackCommandChanged(BindableObject bindableObject, object oldValue, object newValue)
  {
    if (bindableObject is PageLayout view)
    {
      view.BackItem.Command = newValue as ICommand ?? view._GoBackCommand;
    }
  }

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

  private Task GoBackAsync() => _NavigationService.GoToAsync("..", true);

  public ICollection<PageMenuItem> MenuItems => _MenuItems;

  public bool HasBackButton
  {
    get => (bool)GetValue(HasBackButtonProperty);
    set => SetValue(HasBackButtonProperty, value);
  }

  // A dialog never navigates: it completes the result task its caller is awaiting, and the caller pops.
  public ICommand BackCommand
  {
    get => (ICommand)GetValue(BackCommandProperty);
    set => SetValue(BackCommandProperty, value);
  }

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

  public PageLoading Loading
  {
    get => (PageLoading)GetValue(LoadingProperty);
    set => SetValue(LoadingProperty, value);
  }

  public View Footer
  {
    get => (View)GetValue(FooterProperty);
    set => SetValue(FooterProperty, value);
  }

  public bool IsMenuVisible => _MenuItems.Count > 0;

  public bool IsTopMenuVisible => IsMenuVisible && Height >= 0 && Height > Width;

  public bool IsSideMenuVisible => IsMenuVisible && Height >= 0 && Height <= Width;

  public bool IsBackButtonVisible => HasBackButton;

  public bool IsTitleVisible => !string.IsNullOrWhiteSpace(Title);

  public bool IsHintVisible => !string.IsNullOrWhiteSpace(Hint);

  public bool IsHeaderVisible => Header is not null;

  public bool IsFooterVisible => Footer is not null;
}
