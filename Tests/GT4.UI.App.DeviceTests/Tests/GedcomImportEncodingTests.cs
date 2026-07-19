using GT4.UI.Pages;
using Moq;
using System.Text;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers GedcomImportEncoding's non-prompt paths (declared/BOM charsets, ANSEL failure) via the internal
/// Func&lt;Task&lt;Stream&gt;&gt; overload. The codepage-prompt branch needs a live SelectEncodingDialog -- see #122.
/// </summary>
public sealed class GedcomImportEncodingTests
{
  // Fresh MemoryStream per call, mirroring FileResult.OpenReadAsync opening the file anew each time.
  private static Func<Task<Stream>> OpenStream(byte[] bytes) => () => Task.FromResult<Stream>(new MemoryStream(bytes));

  [Fact]
  public async Task ResolveReaderAsync_DeclaredUtf8_DecodesWithoutPromptingAsync()
  {
    const string ged = "0 HEAD\n1 CHAR UTF-8\n0 TRLR";
    var navigation = new Mock<INavigation>();
    var services = new TestServices();
    var gedcomImportEncoding = new GedcomImportEncoding(services.AlertService.Object);

    using var reader = await gedcomImportEncoding.ResolveReaderAsync(OpenStream(Encoding.UTF8.GetBytes(ged)), navigation.Object);

    Assert.NotNull(reader);
    Assert.Equal(ged, (await reader!.ReadToEndAsync()).ReplaceLineEndings("\n"));
    navigation.Verify(n => n.PushModalAsync(It.IsAny<Page>()), Times.Never());
  }

  [Fact]
  public async Task ResolveReaderAsync_Utf8Bom_StripsBomAndDecodesWithoutPromptingAsync()
  {
    const string ged = "0 HEAD\n1 CHAR UTF-8\n0 TRLR";
    var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ged)).ToArray();
    var navigation = new Mock<INavigation>();
    var services = new TestServices();
    var gedcomImportEncoding = new GedcomImportEncoding(services.AlertService.Object);

    using var reader = await gedcomImportEncoding.ResolveReaderAsync(OpenStream(bytes), navigation.Object);

    Assert.NotNull(reader);
    Assert.Equal(ged, (await reader!.ReadToEndAsync()).ReplaceLineEndings("\n"));
    navigation.Verify(n => n.PushModalAsync(It.IsAny<Page>()), Times.Never());
  }

  [Fact]
  public async Task ResolveReaderAsync_DeclaredAnsel_ThrowsWithoutPromptingAsync()
  {
    var bytes = Encoding.UTF8.GetBytes("0 HEAD\n1 CHAR ANSEL\n0 TRLR");
    var navigation = new Mock<INavigation>();
    var services = new TestServices();
    var gedcomImportEncoding = new GedcomImportEncoding(services.AlertService.Object);

    await Assert.ThrowsAsync<NotSupportedException>(() => gedcomImportEncoding.ResolveReaderAsync(OpenStream(bytes), navigation.Object));

    navigation.Verify(n => n.PushModalAsync(It.IsAny<Page>()), Times.Never());
  }
}
