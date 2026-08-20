using GT4.Core.Utils;
using GT4.UI.Abstraction;
using System.Globalization;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class SettingEditorView : ContentView
{
  // The slider is bound even while hidden, so a setting with no range still has to report a legal one.
  private static readonly SettingKind.BoundedNumeric UnboundedRange = new(0, 1, 1, string.Empty);

  private readonly IAlertService _AlertService;

  private bool _Refreshing;

  protected SettingEditorView(IServiceProvider serviceProvider)
  {
    _AlertService = serviceProvider.GetRequiredService<IAlertService>();

    InitializeComponent();
  }

  public SettingEditorView()
    : this(GT4Services.Provider)
  {

  }

  public static readonly BindableProperty EditorProperty = BindableProperty.Create(
    nameof(Editor),
    typeof(ISettingEditor),
    typeof(SettingEditorView),
    default,
    BindingMode.OneWay,
    null,
    OnEditorPropertyChanged);

  private static void OnEditorPropertyChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is SettingEditorView view && oldValue != newValue)
    {
      view.ApplyEditor();
    }
  }

  private void ApplyEditor()
  {
    _Refreshing = true;
    try
    {
      OnPropertyChanged(nameof(Caption));
      OnPropertyChanged(nameof(Description));
      OnPropertyChanged(nameof(IsText));
      OnPropertyChanged(nameof(IsBoolean));
      OnPropertyChanged(nameof(IsBoundedNumeric));
      // Before the value properties: the slider coerces its value against the range it currently has.
      OnPropertyChanged(nameof(ValueMetadata));
      OnValueChanged();
    }
    finally
    {
      _Refreshing = false;
    }
  }

  private void OnValueChanged()
  {
    _Refreshing = true;
    try
    {
      OnPropertyChanged(nameof(Value));
      OnPropertyChanged(nameof(BooleanValue));
      OnPropertyChanged(nameof(NumericValue));
      OnPropertyChanged(nameof(Example));
    }
    finally
    {
      _Refreshing = false;
    }
  }

  public ISettingEditor? Editor => (ISettingEditor?)GetValue(EditorProperty);

  public string Caption => Editor?.DisplayName ?? string.Empty;

  public string Description => Editor?.Description ?? string.Empty;

  public string Example => Editor?.Example ?? string.Empty;

  public bool IsText => Editor?.Kind is SettingKind.Text;

  public bool IsBoolean => Editor?.Kind is SettingKind.Boolean;

  public bool IsBoundedNumeric => Editor?.Kind is SettingKind.BoundedNumeric;

  public SettingKind.BoundedNumeric ValueMetadata =>
    Editor?.Kind as SettingKind.BoundedNumeric ?? UnboundedRange;

  public string Value
  {
    get => Editor?.Value ?? string.Empty;
    set
    {
      // The controls echo back whatever they are given -- the slider even echoes what its current
      // range allows rather than what it was set to.
      if (!_Refreshing && Editor is not null && Editor.Value != value)
      {
        Editor.Value = value;
        OnValueChanged();
      }
    }
  }

  public bool BooleanValue
  {
    get => bool.TryParse(Value, out var value) && value;
    set => Value = value.ToString();
  }

  public double NumericValue
  {
    get
    {
      var metadata = ValueMetadata;
      var text = Value;
      var number = text.EndsWith(metadata.Unit) ? text[..^metadata.Unit.Length] : text;
      return double.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
        ? value
        : metadata.Minimum;
    }
    set
    {
      var metadata = ValueMetadata;
      var steps = Math.Round(value / metadata.Step);
      var snapped = metadata.Step * steps;
      Value = snapped.ToString(CultureInfo.InvariantCulture) + metadata.Unit;
    }
  }

  public ICommand ResetCommand => new SafeCommand(() =>
  {
    Editor?.ResetToDefault();
    OnValueChanged();
  }, _AlertService);
}
