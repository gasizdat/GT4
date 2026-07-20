using GT4.UI.Abstraction;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class MarkdownEditor : ContentView
{
  private readonly ICommand _Command;
  private int _TabIndex;

  protected MarkdownEditor(IServiceProvider serviceProvider)
  {
    _Command = new SafeCommand(OnCommand, serviceProvider.GetRequiredService<IAlertService>());
    _TabIndex = 0;

    InitializeComponent();
  }

  public MarkdownEditor()
    : this(GT4Services.Provider)
  {

  }

  public static readonly BindableProperty MarkdownProperty =
    BindableProperty.Create(nameof(Markdown), typeof(string), typeof(MarkdownView), default, BindingMode.TwoWay, null, OnMarkdownChanged);


  public static readonly BindableProperty PlaceholderProperty =
    BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(MarkdownView), default, BindingMode.OneWay, null, OnPlaceholderChanged);

  public string? Markdown
  {
    get => (string?)GetValue(MarkdownProperty);
    set => SetValue(MarkdownProperty, value);
  }

  public string? Placeholder
  {
    get => (string?)GetValue(PlaceholderProperty);
    set => SetValue(PlaceholderProperty, value);
  }

  public bool DisplayEditor => _TabIndex == 0;

  public bool DisplayHtmlView => _TabIndex == 1;

  // Raised when the "Link a Person" button is tapped; the host page owns person lookup/selection, so this
  // view stays free of any project dependency. The host calls InsertLink back once a person is chosen.
  public event EventHandler? InsertLinkRequested;

  public int TabIndex
  {
    get => _TabIndex;
    set
    {
      if (_TabIndex != value)
      {
        _TabIndex = value;
        OnPropertyChanged(nameof(TabIndex));
        OnPropertyChanged(nameof(DisplayEditor));
        OnPropertyChanged(nameof(DisplayHtmlView));
      }
    }
  }

  public ICommand Command => _Command;

  public void InsertLink(string displayName, int personId)
  {
    var linkText = $"[{displayName}](person:{personId})";
    var cursor = Math.Clamp(TextEditor.CursorPosition, 0, Markdown?.Length ?? 0);
    Markdown = (Markdown ?? string.Empty).Insert(cursor, linkText);
    TextEditor.CursorPosition = cursor + linkText.Length;
  }

  private static void OnMarkdownChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is MarkdownEditor markdownEditor && oldValue != newValue)
    {
      markdownEditor.OnPropertyChanged(nameof(Markdown));
    }
  }

  private static void OnPlaceholderChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is MarkdownEditor markdownEditor && oldValue != newValue)
    {
      markdownEditor.OnPropertyChanged(nameof(Placeholder));
    }
  }

  private void OnCommand(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "Tab0":
        TabIndex = 0;
        break;
      case string commandName when commandName == "Tab1":
        TabIndex = 1;
        break;
      case string commandName when commandName == "InsertLink":
        InsertLinkRequested?.Invoke(this, EventArgs.Empty);
        break;
    }
  }
}