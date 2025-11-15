namespace GT4.UI.Components;

public partial class PageLayout : ContentView
{
  public PageLayout()
  {
    InitializeComponent();
  }


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

  public bool IsHeaderVisible => Header is not null;

  public bool IsFooterVisible => Footer is not null;
}