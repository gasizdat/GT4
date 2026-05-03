using Markdig;

namespace GT4.UI.Components;

public partial class MarkdownView : ContentView
{
  private readonly MarkdownPipeline _MarkdownPipeline;

  public MarkdownView()
  {
    _MarkdownPipeline = new MarkdownPipelineBuilder()
      .UseAdvancedExtensions()
      .UseSoftlineBreakAsHardlineBreak()
      .Build();

    InitializeComponent();
  }

  public static readonly BindableProperty MarkdownProperty =
    BindableProperty.Create(nameof(Markdown), typeof(string), typeof(MarkdownView), default, BindingMode.OneWay, null, OnMarkdownChanged);

  public string? Markdown
  {
    get => (string?)GetValue(MarkdownProperty);
    set => SetValue(MarkdownProperty, value);
  }

  public string HtmlContent => BuildHtmlDocument();

  private static void OnMarkdownChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is MarkdownView view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(HtmlContent));
    }
  }

  private string BuildHtmlDocument()
  {
    string bodyHtml = Markdown is null ? string.Empty : Markdig.Markdown.ToHtml(Markdown, _MarkdownPipeline);
    
    // Keep styling inline so it works offline on all platforms.
    // AppThemeBinding isn't available inside HTML, so we choose neutral colors.
    return
      $@"<!doctype html>
      <html>
      <head>
        <meta charset=""utf-8"">
        <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
        <style>
          :root {{
            color-scheme: light dark;
          }}
          body {{
            font-family: -apple-system, Segoe UI, Roboto, Arial, sans-serif;
            padding: 16px;
            line-height: 1.5;
          }}
          h1, h2, h3 {{ margin-top: 1.0em; }}
          pre {{
            padding: 12px;
            border-radius: 10px;
            overflow-x: auto;
            background: rgba(127,127,127,0.15);
          }}
          code {{
            font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
            font-size: 0.95em;
          }}
          blockquote {{
            border-left: 4px solid rgba(127,127,127,0.35);
            padding-left: 12px;
            margin-left: 0;
            color: rgba(127,127,127,0.9);
          }}
          table {{
            border-collapse: collapse;
          }}
          th, td {{
            border: 1px solid rgba(127,127,127,0.35);
            padding: 6px 10px;
          }}
        </style>
      </head>
      <body>
      {bodyHtml}
      </body>
      </html>";
  }
}