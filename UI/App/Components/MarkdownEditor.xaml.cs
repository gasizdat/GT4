using GT4.UI.Abstraction;
using GT4.UI.Utils;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class MarkdownEditor : ContentView
{
  private readonly ICommand _Command;
  private int _TabIndex;
  private ScrollView? _AncestorScrollView;

  protected MarkdownEditor(IServiceProvider serviceProvider)
  {
    _Command = new SafeCommand(OnCommand, serviceProvider.GetRequiredService<IAlertService>());
    _TabIndex = 0;

    InitializeComponent();

    Loaded += OnLoaded;
    Unloaded += OnUnloaded;
    SizeChanged += OnSizeChanged;
  }

  public MarkdownEditor()
    : this(GT4Services.Provider)
  {

  }

  public static readonly BindableProperty MarkdownProperty =
    BindableProperty.Create(nameof(Markdown), typeof(string), typeof(MarkdownView), default, BindingMode.TwoWay, null, OnMarkdownChanged);


  public static readonly BindableProperty PlaceholderProperty =
    BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(MarkdownView), default, BindingMode.OneWay, null, OnPlaceholderChanged);

  // Forwarded to the inner preview MarkdownView so the "Markdown View" tab renders inserted media links
  // the same way the host's own display does.
  public static readonly BindableProperty MediaResolverProperty =
    BindableProperty.Create(nameof(MediaResolver), typeof(InlineMediaResolver), typeof(MarkdownEditor), null);

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

  public InlineMediaResolver? MediaResolver
  {
    get => (InlineMediaResolver?)GetValue(MediaResolverProperty);
    set => SetValue(MediaResolverProperty, value);
  }

  public ICommand? InsertLinkCommand
  {
    get => (ICommand?)GetValue(InsertLinkCommandProperty);
    set => SetValue(InsertLinkCommandProperty, value);
  }

  public bool DisplayEditor => _TabIndex == 0;

  public bool DisplayPreview => _TabIndex == 1;

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
        OnPropertyChanged(nameof(DisplayPreview));
      }
    }
  }

  public ICommand Command => _Command;

  public View HeaderView => Header;

  public void InsertLink(string displayName, int personId)
  {
    var url = MarkdownLinkUtils.PersonUrl(personId);
    InsertText($"[{displayName}]({url})");
  }

  public void InsertMediaLink(string displayName, int mediaId)
  {
    var url = MarkdownLinkUtils.MediaUrl(mediaId);
    InsertText($"![{displayName}]({url})");
  }

  public void InsertAttachmentLink(string displayName, int attachmentId)
  {
    var url = MarkdownLinkUtils.AttachmentUrl(attachmentId);
    InsertText($"[{displayName}]({url})");
  }

  private void InsertText(string text)
  {
    var cursor = Math.Clamp(TextEditor.CursorPosition, 0, Markdown?.Length ?? 0);
    Markdown = (Markdown ?? string.Empty).Insert(cursor, text);
    TextEditor.CursorPosition = cursor + text.Length;
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

  private void OnLoaded(object? sender, EventArgs e)
  {
    _AncestorScrollView = FindAncestorScrollView();
    if (_AncestorScrollView is not null)
    {
      _AncestorScrollView.Scrolled += OnAncestorScrolled;
    }

    UpdateHeaderPosition();
  }

  private void OnUnloaded(object? sender, EventArgs e)
  {
    if (_AncestorScrollView is not null)
    {
      _AncestorScrollView.Scrolled -= OnAncestorScrolled;
      _AncestorScrollView = null;
    }
  }

  private ScrollView? FindAncestorScrollView()
  {
    for (var element = Parent; element is not null; element = element.Parent)
    {
      if (element is ScrollView scrollView)
      {
        return scrollView;
      }
    }

    return null;
  }

  private void OnAncestorScrolled(object? sender, ScrolledEventArgs e) => UpdateHeaderPosition();

  private void OnSizeChanged(object? sender, EventArgs e) => UpdateHeaderPosition();

  // Keeps the toolbar reachable on a long biography (#446): floats it at the ancestor
  // ScrollView's own visible top once scrolled past, but never lets it rise above its resting
  // position at the editor's own top.
  private void UpdateHeaderPosition()
  {
    if (_AncestorScrollView is null)
    {
      return;
    }

    var editorTop = YRelativeTo(this, _AncestorScrollView);
    Header.TranslationY = Math.Max(0, _AncestorScrollView.ScrollY - editorTop);
  }

  // VisualElement.Y is relative to the immediate parent's own layout, unaffected by scrolling, so
  // summing it up the chain gives a position in the ancestor's own (unscrolled) content space --
  // directly comparable to ScrollView.ScrollY.
  private static double YRelativeTo(Element element, Element ancestor)
  {
    var y = 0.0;
    for (var current = element; current is not null && current != ancestor; current = current.Parent)
    {
      if (current is VisualElement visual)
      {
        y += visual.Y;
      }
    }

    return y;
  }
}
