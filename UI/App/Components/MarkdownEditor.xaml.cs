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

  // The host page owns link insertion (person lookup/selection, and whatever other link types it
  // supports), so this view stays free of any project dependency: the "Link a Person" button (and any
  // future link-type button) executes the host's own command directly, keyed by CommandParameter.
  public static readonly BindableProperty InsertLinkCommandProperty =
    BindableProperty.Create(nameof(InsertLinkCommand), typeof(ICommand), typeof(MarkdownEditor));

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

  public ICommand? InsertLinkCommand
  {
    get => (ICommand?)GetValue(InsertLinkCommandProperty);
    set => SetValue(InsertLinkCommandProperty, value);
  }

  public bool DisplayEditor => _TabIndex == 0;

  public bool DisplayHtmlView => _TabIndex == 1;

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
    }
  }
}