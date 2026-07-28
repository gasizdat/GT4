using GT4.UI.Components;
using Xunit;

namespace GT4.UI.DeviceTests;

public class MarkdownViewTests
{
  [Fact]
  public async Task HtmlContent_renders_a_person_link_as_a_person_scheme_anchor()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var view = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownView
    {
      Markdown = "See [Jane Doe](person:123) for details."
    });

    Assert.Contains("href=\"person:123\"", view.HtmlContent);
    Assert.Contains("Jane Doe", view.HtmlContent);
  }

  [Fact]
  public async Task HtmlContent_rewrites_a_media_reference_into_its_data_uri()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var view = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownView
    {
      Markdown = "![A caption](media:11)",
      MediaSources = new Dictionary<int, string> { [11] = "data:image/png;base64,AAA=" }
    });

    Assert.Contains("src=\"data:image/png;base64,AAA=\"", view.HtmlContent);
    Assert.DoesNotContain("media:11", view.HtmlContent);
  }

  [Fact]
  public async Task HtmlContent_renders_an_attachment_reference_as_an_attachment_scheme_anchor()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var view = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownView
    {
      Markdown = "See [scan.pdf](attachment:20) for details."
    });

    Assert.Contains("href=\"attachment:20\"", view.HtmlContent);
    Assert.Contains("scan.pdf", view.HtmlContent);
  }
}
