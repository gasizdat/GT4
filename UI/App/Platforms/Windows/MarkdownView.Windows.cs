using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;

namespace GT4.UI.Components;

// WinUI hosts WebView2 content out-of-process, so wheel/touch scroll over the bio never bubbles to the page
// ScrollView, and links/text are unreachable while the view is input-transparent. On Windows we make the
// view interactive (links open externally, text selects) and forward the scroll deltas the HTML posts back
// to the nearest parent ScrollViewer, restoring page scrolling. Other platforms keep the XAML pass-through.
public partial class MarkdownView
{
  partial void ConfigurePlatformInput()
  {
    Root.InputTransparent = false;
    InternalWebView.InputTransparent = false;
    InternalWebView.HandlerChanged += OnWebViewHandlerChanged;
  }

  private void OnWebViewHandlerChanged(object? sender, EventArgs e)
  {
    if (InternalWebView.Handler?.PlatformView is not WebView2 webView2)
      return;

    webView2.WebMessageReceived -= OnWebMessageReceived;
    webView2.WebMessageReceived += OnWebMessageReceived;
  }

  private void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
  {
    if (!double.TryParse(args.TryGetWebMessageAsString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var deltaY))
      return;

    var scrollViewer = FindParentScrollViewer(sender);
    scrollViewer?.ChangeView(null, scrollViewer.VerticalOffset + deltaY, null, true);
  }

  private static ScrollViewer? FindParentScrollViewer(DependencyObject element)
  {
    for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
    {
      if (parent is ScrollViewer scrollViewer)
        return scrollViewer;
    }

    return null;
  }
}
