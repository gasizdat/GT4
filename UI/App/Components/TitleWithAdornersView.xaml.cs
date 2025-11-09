namespace GT4.UI.App.Components;

public partial class TitleWithAdornersView : ContentView
{
	public TitleWithAdornersView()
	{
		InitializeComponent();
	}

  public static readonly BindableProperty TitleTextProperty =
    BindableProperty.Create(nameof(TitleText), typeof(string), typeof(TitleWithAdornersView), default, BindingMode.OneWay);

  public static readonly BindableProperty SubtitleTextProperty =
    BindableProperty.Create(nameof(SubtitleText), typeof(string), typeof(TitleWithAdornersView), default, BindingMode.OneWay, null, OnSubtitleChanged);

  public static readonly BindableProperty RemoveAdornerVisibilityProperty =
    BindableProperty.Create(nameof(RemoveAdornerVisibility), typeof(bool), typeof(TitleWithAdornersView), default, BindingMode.OneWay);

  public static readonly BindableProperty EditAdornerVisibilityProperty =
    BindableProperty.Create(nameof(EditAdornerVisibility), typeof(bool), typeof(TitleWithAdornersView), default, BindingMode.OneWay);

  public string? TitleText => GetValue(TitleTextProperty) as string;
  public string? SubtitleText => GetValue(TitleTextProperty) as string;
  public bool IsSubtitleTextVisible => !string.IsNullOrWhiteSpace(SubtitleText);
  public bool RemoveAdornerVisibility => (bool)GetValue(RemoveAdornerVisibilityProperty);
  public bool EditAdornerVisibility => (bool)GetValue(EditAdornerVisibilityProperty);

  private static void OnSubtitleChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is TitleWithAdornersView view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(IsSubtitleTextVisible));
    }
  }
}