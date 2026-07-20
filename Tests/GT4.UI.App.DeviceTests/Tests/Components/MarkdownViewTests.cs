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
}
