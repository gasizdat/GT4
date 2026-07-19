using FluentAssertions;
using GT4.Core.Project;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace GT4.Core.Gedcom.Tests;

public sealed class GedcomCharsetTests : IAsyncLifetime
{
  private const string HeaderTemplate =
    """
    0 HEAD
    1 CHAR {0}
    0 TRLR
    """;

  private readonly List<string> _paths = [];
  private static CancellationToken Token => TestContext.Current.CancellationToken;
  private readonly GedcomImporter _importer = new(new FileGedcomMediaReader());

  [ModuleInitializer]
  internal static void RegisterCodePages() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

  public ValueTask InitializeAsync() => ValueTask.CompletedTask;

  public ValueTask DisposeAsync()
  {
    foreach (var path in _paths)
    {
      foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
      {
        try { File.Delete(path + suffix); } catch { /* best-effort temp cleanup */ }
      }
    }
    return ValueTask.CompletedTask;
  }

  [Fact]
  public async Task Detect_Utf8Bom_ReturnsUtf8WithoutPromptingAsync()
  {
    var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Header("UTF-8"))).ToArray();

    var result = await Detect(bytes);

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.UTF8);
  }

  [Fact]
  public async Task Detect_Utf16LeBom_ReturnsUnicodeAsync()
  {
    var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Header("UNICODE"))).ToArray();

    var result = await Detect(bytes);

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.Unicode);
  }

  [Fact]
  public async Task Detect_Utf16BeBom_ReturnsBigEndianUnicodeAsync()
  {
    var bytes = Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(Header("UNICODE"))).ToArray();

    var result = await Detect(bytes);

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.BigEndianUnicode);
  }

  [Fact]
  public async Task Detect_CharLikeLineAfterHeader_IsIgnoredAsync()
  {
    // CHAR is only ever meaningful as a level-1 child of HEAD; a same-shaped line belonging to a later
    // record must not be mistaken for it. Pins the scan bound in FindDeclaredCharsetAsync (stops at the
    // second level-0 line) rather than scanning arbitrarily far into the file for a "1 CHAR " prefix.
    var ged =
      """
      0 HEAD
      1 SOUR GT4
      0 @S1@ SOUR
      1 CHAR ANSI
      0 TRLR
      """;

    var result = await Detect(Encoding.UTF8.GetBytes(ged));

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.UTF8);
  }

  [Fact]
  public async Task Detect_DeclaredUtf8NoBom_ReturnsUtf8Async()
  {
    var result = await Detect(Encoding.UTF8.GetBytes(Header("UTF-8")));

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.UTF8);
  }

  [Fact]
  public async Task Detect_DeclaredAscii_ReturnsAsciiAsync()
  {
    var result = await Detect(Encoding.UTF8.GetBytes(Header("ASCII")));

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.ASCII);
  }

  [Fact]
  public async Task Detect_MissingCharLine_DefaultsToUtf8Async()
  {
    var result = await Detect(Encoding.UTF8.GetBytes("0 HEAD\n0 TRLR"));

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.UTF8);
  }

  [Fact]
  public async Task Detect_EmptyFile_DefaultsToUtf8Async()
  {
    var result = await Detect([]);

    result.NeedsCodepage.Should().BeFalse();
    result.Encoding.Should().Be(Encoding.UTF8);
  }

  [Fact]
  public async Task Detect_DeclaredAnsi_NeedsCodepageAsync()
  {
    var result = await Detect(Encoding.UTF8.GetBytes(Header("ANSI")));

    result.NeedsCodepage.Should().BeTrue();
    result.Encoding.Should().BeNull();
    result.DeclaredValue.Should().Be("ANSI");
  }

  [Fact]
  public async Task Detect_DeclaredAnsel_ThrowsAsync()
  {
    var act = () => Detect(Encoding.UTF8.GetBytes(Header("ANSEL")));

    await act.Should().ThrowAsync<NotSupportedException>();
  }

  [Fact]
  public async Task Cp1251AnsiFile_ImportsCyrillicNameCorrectlyOnceDecodedWithTheChosenCodepage()
  {
    var cp1251 = Encoding.GetEncoding(1251);
    var ged =
      """
      0 HEAD
      1 CHAR ANSI
      0 @I1@ INDI
      1 NAME Рюрик /Новгородский/
      1 SEX M
      0 TRLR
      """;
    var bytes = cp1251.GetBytes(ged);

    // This is the path GedcomImportEncoding drives in the app: detect first (confirms the file needs a
    // codepage prompt), then decode with the codepage the user picked and hand the result to the importer.
    var result = await Detect(bytes);
    result.NeedsCodepage.Should().BeTrue();

    var path = Path.Combine(Path.GetTempPath(), $"gt4_charset_{Guid.NewGuid():N}.db");
    _paths.Add(path);
    await using var document = await ProjectDocument.CreateNewAsync(path, "gedcom", Token);
    using var reader = new StreamReader(new MemoryStream(bytes), cp1251);
    await _importer.ImportAsync(document, reader, Token);

    var byName = await GedcomTestGraph.PersonsByNameAsync(document, Token);
    byName.Should().ContainKey("Рюрик Новгородский");
  }

  private static Task<GedcomCharsetResult> Detect(byte[] bytes) => GedcomCharset.DetectAsync(new MemoryStream(bytes), Token);

  private static string Header(string charset) => string.Format(HeaderTemplate, charset);
}
