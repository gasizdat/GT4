namespace GT4.UI.Behaviors;

public sealed class FocusOnTrueBehavior : Behavior<VisualElement>
{
  public static readonly BindableProperty IsFocusedProperty =
      BindableProperty.Create(
          nameof(IsFocused),
          typeof(bool),
          typeof(FocusOnTrueBehavior),
          false,
          BindingMode.TwoWay,
          propertyChanged: OnIsFocusedChanged);

  /// <summary>
  /// When set to true, behavior will focus the attached element.
  /// </summary>
  public bool IsFocused
  {
    get => (bool)GetValue(IsFocusedProperty);
    set => SetValue(IsFocusedProperty, value);
  }

  public static readonly BindableProperty AutoResetProperty =
      BindableProperty.Create(
          nameof(AutoReset),
          typeof(bool),
          typeof(FocusOnTrueBehavior),
          true);

  /// <summary>
  /// If true, the behavior will set IsFocused back to false after a successful focus.
  /// Default true to allow "pulse" focusing multiple times.
  /// </summary>
  public bool AutoReset
  {
    get => (bool)GetValue(AutoResetProperty);
    set => SetValue(AutoResetProperty, value);
  }

  VisualElement? _associated;
  bool _isLoaded;

  protected override void OnAttachedTo(VisualElement bindable)
  {
    base.OnAttachedTo(bindable);

    _associated = bindable;

    bindable.Loaded += OnLoaded;
    bindable.Unloaded += OnUnloaded;

    TryFocusIfNeeded();
  }

  protected override void OnDetachingFrom(VisualElement bindable)
  {
    bindable.Loaded -= OnLoaded;
    bindable.Unloaded -= OnUnloaded;

    _associated = null;
    _isLoaded = false;

    base.OnDetachingFrom(bindable);
  }

  static void OnIsFocusedChanged(BindableObject bindable, object oldValue, object newValue)
  {
    var behavior = (FocusOnTrueBehavior)bindable;

    if (newValue is true)
      behavior.TryFocusIfNeeded();
  }

  void OnLoaded(object? sender, EventArgs e)
  {
    _isLoaded = true;
    TryFocusIfNeeded();
  }

  void OnUnloaded(object? sender, EventArgs e) => _isLoaded = false;

  void TryFocusIfNeeded()
  {
    if (_associated is null)
      return;

    if (!IsFocused)
      return;

    // Focusing an element not yet in the platform visual tree is undefined behaviour, and Loaded is
    // what says it is in there.
    if (!_isLoaded)
      return;

    if (!_associated.IsVisible || !_associated.IsEnabled)
      return;

    _associated.Dispatcher.Dispatch(() =>
    {
      var focused = _associated.Focus();

      if (focused && AutoReset)
        IsFocused = false;
    });
  }
}
