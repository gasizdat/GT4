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

  public void InsertMediaLink(string displayName, int mediaId, int? widthPercent)
  {
    var url = MarkdownLinkUtils.MediaUrl(mediaId);
    var description = MarkdownLinkUtils.ImageDescription(displayName, widthPercent);
    InsertText($"![{description}]({url})");
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

  private void WrapSelection(string marker)
  {
    var text = Markdown ?? string.Empty;
    var start = Math.Clamp(TextEditor.CursorPosition, 0, text.Length);
    var length = Math.Clamp(TextEditor.SelectionLength, 0, text.Length - start);
    var wrapped = text.Insert(start + length, marker).Insert(start, marker);
    ReplaceText(wrapped, start + marker.Length, length);
  }

  private void PrefixSelectedLines(string prefix)
  {
    var text = Markdown ?? string.Empty;
    var start = Math.Clamp(TextEditor.CursorPosition, 0, text.Length);
    var length = Math.Clamp(TextEditor.SelectionLength, 0, text.Length - start);
    var firstLineStart = start;
    while (!IsLineStart(text, firstLineStart))
    {
      firstLineStart--;
    }

    // A selection ending right at a line's start doesn't take that line in.
    var lineStarts = Enumerable.Range(start + 1, Math.Max(0, length - 1))
      .Where(position => IsLineStart(text, position))
      .Prepend(firstLineStart)
      .ToArray();
    var prefixed = Enumerable.Reverse(lineStarts).Aggregate(text, (result, lineStart) => result.Insert(lineStart, prefix));
    ReplaceText(prefixed, start + prefix.Length, length + prefix.Length * (lineStarts.Length - 1));
  }

  private void ReplaceText(string markdown, int selectionStart, int selectionLength)
  {
    // Writing the text drops the native selection without updating SelectionLength, so an
    // unchanged SelectionLength set afterwards would never reach the native control.
    TextEditor.SelectionLength = 0;
    Markdown = markdown;
    TextEditor.CursorPosition = selectionStart;
    TextEditor.SelectionLength = selectionLength;
    TextEditor.Focus();
  }

  // WinUI's TextBox separates lines with a bare '\r'; imported text may carry "\r\n" or '\n'.
  private static bool IsLineStart(string text, int position) =>
    position == 0
    || text[position - 1] == '\n'
    || (text[position - 1] == '\r' && (position == text.Length || text[position] != '\n'));

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
      case string commandName when commandName == "FormatBold":
        WrapSelection("**");
        break;
      case string commandName when commandName == "FormatItalic":
        WrapSelection("*");
        break;
      case string commandName when commandName == "FormatHeading":
        PrefixSelectedLines("## ");
        break;
      case string commandName when commandName == "FormatBulletList":
        PrefixSelectedLines("- ");
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
