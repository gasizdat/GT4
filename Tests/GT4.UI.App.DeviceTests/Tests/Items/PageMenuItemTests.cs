using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

public class PageMenuItemTests
{
  [Fact]
  public void Text_splits_at_the_first_space_into_the_button_glyph_and_the_tooltip()
  {
    var item = new PageMenuItem { Text = "📝 To Names" };

    Assert.Equal("📝", item.ButtonText);
    Assert.Equal("To Names", item.ToolTipText);
  }

  [Fact]
  public void Only_the_first_space_splits_a_multi_word_caption()
  {
    var item = new PageMenuItem { Text = "❌ Remove 'Sample Project'" };

    Assert.Equal("❌", item.ButtonText);
    Assert.Equal("Remove 'Sample Project'", item.ToolTipText);
  }

  [Fact]
  public void A_caption_without_a_glyph_still_reaches_the_tooltip()
  {
    var item = new PageMenuItem { Text = "Refresh" };

    Assert.Equal("?", item.ButtonText);
    Assert.Equal("Refresh", item.ToolTipText);
  }
}
