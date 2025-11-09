using System.Windows.Input;

namespace GT4.UI.App.Components;

public partial class TitleWithAdornersView : ContentView
{
  public TitleWithAdornersView()
  {
    InitializeComponent();
  }

  // ===== Bindable Properties =====

  public static readonly BindableProperty TitleTextProperty =
      BindableProperty.Create(
          nameof(TitleText),
          typeof(string),
          typeof(TitleWithAdornersView),
          default(string),
          BindingMode.OneWay);

  public static readonly BindableProperty SubtitleTextProperty =
      BindableProperty.Create(
          nameof(SubtitleText),
          typeof(string),
          typeof(TitleWithAdornersView),
          default(string),
          BindingMode.OneWay,
          propertyChanged: OnSubtitleChanged);

  public static readonly BindableProperty RemoveAdornerVisibilityProperty =
      BindableProperty.Create(
          nameof(RemoveAdornerVisibility),
          typeof(bool),
          typeof(TitleWithAdornersView),
          false,
          BindingMode.OneWay);

  public static readonly BindableProperty EditAdornerVisibilityProperty =
      BindableProperty.Create(
          nameof(EditAdornerVisibility),
          typeof(bool),
          typeof(TitleWithAdornersView),
          false,
          BindingMode.OneWay);

  public static readonly BindableProperty EditAdornerCommandProperty =
      BindableProperty.Create(
          nameof(EditAdornerCommand),
          typeof(ICommand),
          typeof(TitleWithAdornersView));

  public static readonly BindableProperty DeleteAdornerCommandProperty =
      BindableProperty.Create(
          nameof(DeleteAdornerCommand),
          typeof(ICommand),
          typeof(TitleWithAdornersView));

  public static readonly BindableProperty AdornerCommandParameterProperty =
      BindableProperty.Create(
          nameof(AdornerCommandParameter),
          typeof(object),
          typeof(TitleWithAdornersView));

  public string? TitleText
  {
    get => (string?)GetValue(TitleTextProperty);
    set => SetValue(TitleTextProperty, value);
  }

  public string? SubtitleText
  {
    get => (string?)GetValue(SubtitleTextProperty);
    set => SetValue(SubtitleTextProperty, value);
  }

  public bool RemoveAdornerVisibility
  {
    get => (bool)GetValue(RemoveAdornerVisibilityProperty);
    set => SetValue(RemoveAdornerVisibilityProperty, value);
  }

  public bool EditAdornerVisibility
  {
    get => (bool)GetValue(EditAdornerVisibilityProperty);
    set => SetValue(EditAdornerVisibilityProperty, value);
  }

  public ICommand? EditAdornerCommand
  {
    get => (ICommand?)GetValue(EditAdornerCommandProperty);
    set => SetValue(EditAdornerCommandProperty, value);
  }

  public ICommand? DeleteAdornerCommand
  {
    get => (ICommand?)GetValue(DeleteAdornerCommandProperty);
    set => SetValue(DeleteAdornerCommandProperty, value);
  }

  public object? AdornerCommandParameter
  {
    get => GetValue(AdornerCommandParameterProperty);
    set => SetValue(AdornerCommandParameterProperty, value);
  }

  public bool IsSubtitleTextVisible => !string.IsNullOrWhiteSpace(SubtitleText);


  // ===== Public events to bubble up user interaction =====

  public event EventHandler? EditAdornerClicked;
  public event EventHandler? DeleteAdornerClicked;

  private static void OnSubtitleChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is TitleWithAdornersView view && !Equals(oldValue, newValue))
    {
      view.OnPropertyChanged(nameof(IsSubtitleTextVisible));
    }
  }

  private void OnEditAdornerClicked(object? sender, EventArgs e)
  {
    EditAdornerClicked?.Invoke(this, EventArgs.Empty);

    if (EditAdornerCommand?.CanExecute(AdornerCommandParameter) == true)
    {
      EditAdornerCommand.Execute(AdornerCommandParameter);
    }
  }

  private void OnDeleteAdornerClicked(object? sender, EventArgs e)
  {
    DeleteAdornerClicked?.Invoke(this, EventArgs.Empty);

    if (DeleteAdornerCommand?.CanExecute(AdornerCommandParameter) == true)
    {
      DeleteAdornerCommand.Execute(AdornerCommandParameter);
    }
  }
}
