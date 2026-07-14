using GT4.Core.Utils;
using GT4.UI.Abstraction;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class SettingEditorView : ContentView
{
  private readonly IAlertService _AlertService;

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
      view.OnPropertyChanged(nameof(Caption));
      view.OnPropertyChanged(nameof(Description));
      view.OnPropertyChanged(nameof(Example));
      view.OnPropertyChanged(nameof(Value));
    }
  }

  public ISettingEditor? Editor => (ISettingEditor?)GetValue(EditorProperty);

  public string Caption => Editor?.DisplayName ?? string.Empty;

  public string Description => Editor?.Description ?? string.Empty;

  public string Example => Editor?.Example ?? string.Empty;

  public string Value
  {
    get => Editor?.Value ?? string.Empty;
    set
    {
      if (Editor is not null && Editor.Value != value)
      {
        Editor.Value = value;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Example));
      }
    }
  }

  public ICommand ResetCommand => new SafeCommand(() =>
  {
    Editor?.ResetToDefault();
    OnPropertyChanged(nameof(Value));
    OnPropertyChanged(nameof(Example));
  }, _AlertService);
}