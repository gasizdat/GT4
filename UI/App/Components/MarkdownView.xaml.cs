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
    ConfigurePlatformInput();
  }

  // Windows makes the WebView interactive and forwards scroll here; other platforms keep the XAML's
  // input-transparent pass-through (no implementation, so the call is elided).
  partial void ConfigurePlatformInput();

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

  // A tapped link (e.g. the Google Maps coordinate reference) opens in the external browser/maps app rather
  // than navigating inside the display WebView. The inline HTML document loads without an http(s) URL, so
  // only real links are intercepted; everything else (the initial content load) proceeds untouched.
  private async void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
  {
    if (!IsExternalLink(e.Url))
      return;

    e.Cancel = true;
    try
    {
      await Launcher.Default.OpenAsync(e.Url);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  private static bool IsExternalLink(string url) =>
    url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

  private async void OnWebViewNavigated(object sender, WebNavigatedEventArgs e)
  {
    if (e.Result != WebNavigationResult.Success) return;

    // Small delay to ensure the DOM has rendered completely
    await Task.Delay(100);

    // JavaScript to get the height of the body
    string result = await InternalWebView.EvaluateJavaScriptAsync(
        "Math.max(document.body.scrollHeight, document.body.offsetHeight, " +
        "document.documentElement.clientHeight, document.documentElement.scrollHeight, " +
        "document.documentElement.offsetHeight).toString()");

    if (double.TryParse(result, out double height))
    {
      // Update the HeightRequest of the WebView
      // Note: You might need to add a small buffer (e.g., +20) 
      // to prevent tiny scrollbars on some platforms.
      InternalWebView.HeightRequest = height;
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
          html, body {{overflow: hidden;    /* Removes internal scrollbars */
              touch-action: pan-y;          /* Allows vertical touch gestures to bubble up */
              height: auto;
          }}
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
      <script>
        (function () {{
          var host = window.chrome && window.chrome.webview;
          if (!host) return;                       /* WinUI WebView2 only; mobile scrolls natively */
          function forward(delta) {{ host.postMessage(delta.toString()); }}
          window.addEventListener('wheel', function (e) {{ forward(e.deltaY); }}, {{ passive: true }});
          var lastY = null;
          window.addEventListener('touchstart', function (e) {{ lastY = e.touches.length ? e.touches[0].clientY : null; }}, {{ passive: true }});
          window.addEventListener('touchmove', function (e) {{
            if (lastY === null || !e.touches.length) return;
            var y = e.touches[0].clientY;
            forward(lastY - y);
            lastY = y;
          }}, {{ passive: true }});
        }})();
      </script>
      </body>
      </html>";
  }
}