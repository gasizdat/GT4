using GT4.UI.Components;
using Xunit;

namespace GT4.UI.DeviceTests;

public class MarkdownEditorFormattingTests
{
  [Theory]
  [InlineData("FormatBold", "one **two** three")]
  [InlineData("FormatItalic", "one *two* three")]
  public async Task Wrap_EnclosesTheSelection_AndKeepsItSelected(string command, string expected)
  {
    var (markdown, cursor, selection) = await FormatAsync("one two three", 4, 3, command);

    Assert.Equal(expected, markdown);
    Assert.Equal("two", markdown.Substring(cursor, selection));
  }

  [Fact]
  public async Task Wrap_WithNoSelection_PutsTheCursorBetweenTheMarkers()
  {
    var (markdown, cursor, selection) = await FormatAsync("one ", 4, 0, "FormatBold");

    Assert.Equal("one ****", markdown);
    Assert.Equal(6, cursor);
    Assert.Equal(0, selection);
  }

  [Theory]
  [InlineData("FormatHeading", "## ")]
  [InlineData("FormatBulletList", "- ")]
  public async Task Prefix_GoesAtTheStartOfTheCursorLine_NotAtTheCursor(string command, string prefix)
  {
    var (markdown, cursor, _) = await FormatAsync("first\nsecond line", 9, 0, command);

    Assert.Equal($"first\n{prefix}second line", markdown);
    Assert.Equal(9 + prefix.Length, cursor);
  }

  [Fact]
  public async Task Prefix_AtTheVeryStart_PrefixesTheFirstLine()
  {
    var (markdown, _, _) = await FormatAsync("first\nsecond", 0, 0, "FormatBulletList");

    Assert.Equal("- first\nsecond", markdown);
  }

  [Theory]
  [InlineData("first\rsecond", "first\r- second")]
  [InlineData("first\r\nsecond", "first\r\n- second")]
  public async Task Prefix_RecognizesCarriageReturnLineBreaks(string text, string expected)
  {
    var secondLine = text.IndexOf("second");

    var (markdown, _, _) = await FormatAsync(text, secondLine + 2, 0, "FormatBulletList");

    Assert.Equal(expected, markdown);
  }

  [Fact]
  public async Task Prefix_PrefixesEverySelectedLine_AndKeepsTheSameTextSelected()
  {
    var (markdown, cursor, selection) = await FormatAsync("a\nbb\ncc\nd", 3, 4, "FormatBulletList");

    Assert.Equal("a\n- bb\n- cc\nd", markdown);
    Assert.Equal("b\n- cc", markdown.Substring(cursor, selection));
  }

  [Fact]
  public async Task Prefix_SkipsALineTheSelectionEndsRightBefore()
  {
    var (markdown, _, _) = await FormatAsync("aa\nbb\ncc", 3, 3, "FormatBulletList");

    Assert.Equal("aa\n- bb\ncc", markdown);
  }

#if WINDOWS
  [Fact]
  public async Task Wrap_KeepsTheNativeSelection_OnTheWrappedText()
  {
    var (editor, textEditor, window) = await AttachAsync("one two three");
    await using var _ = window;
    var textBox = (Microsoft.UI.Xaml.Controls.TextBox)textEditor.Handler!.PlatformView!;
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      textEditor.CursorPosition = 4;
      textEditor.SelectionLength = 3;
    });

    await MainThread.InvokeOnMainThreadAsync(() => editor.Command.Execute("FormatBold"));

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => (textBox.Text, textBox.SelectedText)),
      state => state == ("one **two** three", "two"),
      timeoutMessage: "The native TextBox lost the selection across the text rewrite.");
  }

  [Fact]
  public async Task Wrap_HandsFocusBackToTheEditor()
  {
    var (editor, textEditor, window) = await AttachAsync("one ");
    await using var _ = window;

    await MainThread.InvokeOnMainThreadAsync(() => editor.Command.Execute("FormatBold"));

    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => textEditor.IsFocused),
      focused => focused,
      timeoutMessage: "The Editor was left unfocused, so typing wouldn't land between the markers.");
  }

  [Fact]
  public async Task Header_WrapsItsButtons_RatherThanClippingThem_AtPhoneWidth()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var editor = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownEditor());
    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage
    {
      Content = new Grid { WidthRequest = 330, HorizontalOptions = LayoutOptions.Start, Children = { editor } },
    });
    await using var window = await WindowHost.AttachAsync(page);

    var header = (Layout)editor.HeaderView;
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() =>
        (header.Width, Overflowing: header.Children.OfType<View>().Count(child => child.Frame.Right > header.Width))),
      layout => layout.Width is > 0 and <= 330 && layout.Overflowing == 0,
      timeoutMessage: "The header's buttons still run past its right edge at phone width.");
  }

  private static async Task<(MarkdownEditor Editor, Editor TextEditor, IAsyncDisposable Window)> AttachAsync(string markdown)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var editor = await MainThread.InvokeOnMainThreadAsync(() => new MarkdownEditor { Markdown = markdown });
    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = editor });
    var window = await WindowHost.AttachAsync(page);
    var textEditor = await MainThread.InvokeOnMainThreadAsync(() => editor.GetVisualTreeDescendants().OfType<Editor>().Single());
    await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => textEditor.IsLoaded),
      loaded => loaded,
      timeoutMessage: "The Editor never loaded.");
    return (editor, textEditor, window);
  }
#endif

  private static async Task<(string Markdown, int Cursor, int Selection)> FormatAsync(
    string markdown, int cursor, int selection, string command)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var markdownEditor = new MarkdownEditor { Markdown = markdown };
      var textEditor = markdownEditor.GetVisualTreeDescendants().OfType<Editor>().Single();
      textEditor.CursorPosition = cursor;
      textEditor.SelectionLength = selection;

      markdownEditor.Command.Execute(command);

      return (markdownEditor.Markdown!, textEditor.CursorPosition, textEditor.SelectionLength);
    });
  }
}
