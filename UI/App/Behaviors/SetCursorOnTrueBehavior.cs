namespace GT4.UI.Behaviors;

public enum CursorPosition
{
  Begin,
  End,
  Middle
}

public sealed class SetCursorOnTrueBehavior : Behavior<InputView>
{
  public static readonly BindableProperty IsCursorSetProperty =
      BindableProperty.Create(
          nameof(IsCursorSet),
          typeof(bool),
          typeof(SetCursorOnTrueBehavior),
          false,
          BindingMode.TwoWay,
          propertyChanged: IsCursorSetChanged);

  /// <summary>
  /// When set to true, behavior will focus the attached element.
  /// </summary>
  public bool IsCursorSet
  {
    get => (bool)GetValue(IsCursorSetProperty);
    set => SetValue(IsCursorSetProperty, value);
  }

  public static readonly BindableProperty AutoResetProperty =
      BindableProperty.Create(
          nameof(AutoReset),
          typeof(bool),
          typeof(SetCursorOnTrueBehavior),
          true);

  /// <summary>
  /// If true, the behavior will set IsCursorSet back to false after a successful focus.
  /// Default true to allow "pulse" focusing multiple times.
  /// </summary>
  public bool AutoReset
  {
    get => (bool)GetValue(AutoResetProperty);
    set => SetValue(AutoResetProperty, value);
  }

  public static readonly BindableProperty CursorPositionProperty =
      BindableProperty.Create(
          nameof(IsCursorSet),
          typeof(CursorPosition),
          typeof(SetCursorOnTrueBehavior),
          Behaviors.CursorPosition.End,
          BindingMode.TwoWay,
          propertyChanged: IsCursorSetChanged);

  public CursorPosition CursorPosition
  {
    get => (CursorPosition)GetValue(CursorPositionProperty);
    set => SetValue(CursorPositionProperty, value);
  }

  InputView? _InputView;
  bool _IsLoaded;

  protected override void OnAttachedTo(InputView inputView)
  {
    base.OnAttachedTo(inputView);

    _InputView = inputView;

    inputView.Loaded += OnLoaded;
    inputView.Unloaded += OnUnloaded;

    TrySetCursorIfNeeded();
  }

  protected override void OnDetachingFrom(InputView inputView)
  {
    inputView.Loaded -= OnLoaded;
    inputView.Unloaded -= OnUnloaded;

    _InputView = null;
    _IsLoaded = false;

    base.OnDetachingFrom(inputView);
  }

  static void IsCursorSetChanged(BindableObject bindable, object oldValue, object newValue)
  {
    var behavior = (SetCursorOnTrueBehavior)bindable;

    if (newValue is true)
      behavior.TrySetCursorIfNeeded();
  }

  void OnLoaded(object? sender, EventArgs e)
  {
    _IsLoaded = true;
    TrySetCursorIfNeeded();
  }

  void OnUnloaded(object? sender, EventArgs e) => _IsLoaded = false;

  void TrySetCursorIfNeeded()
  {
    if (_InputView is null)
      return;

    if (!IsCursorSet)
      return;

    // Reaching into an element not yet in the platform visual tree is undefined behaviour, and
    // Loaded is what says it is in there.
    if (!_IsLoaded)
      return;

    if (!_InputView.IsVisible || !_InputView.IsEnabled)
      return;

    _InputView.Dispatcher.Dispatch(() =>
    {
      var position = CursorPosition switch
      {
        CursorPosition.Begin => 0,
        CursorPosition.Middle => _InputView.Text.Length / 2,
        _ => _InputView.Text.Length
      };
      _InputView.CursorPosition = position;

      if (AutoReset)
        IsCursorSet = false;
    });
  }
}
