using GT4.UI.Pages;
using Moq;
using System.Text;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers the non-prompt paths of GedcomImportEncoding's detect-and-decode logic (declared/BOM charsets
/// that resolve without asking the user, and the ANSEL failure) through the internal Func&lt;Task&lt;Stream&gt;&gt;
/// overload of ResolveReaderAsync, the seam split out of the FileResult overload for exactly this:
/// FileResult.OpenReadAsync isn't virtual and its path-based constructor doesn't produce a working instance
/// on Windows, so the FileResult-reading half isn't unit-testable at all. The codepage-prompt branch needs a
/// live SelectEncodingDialog and isn't covered here -- see #122.
/// </summary>
public sealed class GedcomImportEncodingTests
{
  // A fresh MemoryStream per call, mirroring FileResult.OpenReadAsync's real behaviour of opening the file
  // from the start every time (verified empirically: both Windows and Android read from a re-openable file
  // path, never a single-use stream).
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
