using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Converters;
using Xunit;

namespace GT4.UI.DeviceTests;

public class AttachmentDataConverterTests
{
  [Fact]
  public async Task ToObjectAsync_returns_null_for_a_null_attachment()
  {
    var result = await new AttachmentDataConverter().ToObjectAsync(null, CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task ToObjectAsync_returns_the_decoded_file_name()
  {
    var content = GedcomPhotoResidue.EncodeAttachment([1, 2, 3], "deed.pdf");
    var attachment = new Data(1, content, "application/pdf", DataCategory.PersonAttachment);

    var result = await new AttachmentDataConverter().ToObjectAsync(attachment, CancellationToken.None);

    Assert.Equal("deed.pdf", result);
  }

  [Fact]
  public async Task FromObjectAsync_returns_null_for_input_that_is_not_an_AttachmentPick()
  {
    var result = await new AttachmentDataConverter().FromObjectAsync("not a pick", CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task FromObjectAsync_encodes_the_picked_bytes_and_file_name()
  {
    var pick = new AttachmentPick([9, 8, 7], "scan.pdf", "application/pdf");

    var result = await new AttachmentDataConverter().FromObjectAsync(pick, CancellationToken.None);

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonAttachment, result.Category);
    Assert.Equal("application/pdf", result.MimeType);
    Assert.Equal([9, 8, 7], GedcomPhotoResidue.ExtractImageBytes(result.Content));
    Assert.Equal("scan.pdf", await GedcomPhotoResidue.ExtractFileNameAsync(result, CancellationToken.None));
  }

  [Fact]
  public async Task FromObjectAsync_falls_back_to_octet_stream_when_the_picker_reports_no_content_type()
  {
    // A null/empty MimeType would emit no FORM on export, which reimports as a photo instead of an
    // attachment -- so a picked file with no usable content type still must not stay null here.
    var pick = new AttachmentPick([1], "unknown.bin", null);

    var result = await new AttachmentDataConverter().FromObjectAsync(pick, CancellationToken.None);

    Assert.Equal("application/octet-stream", result?.MimeType);
  }
}
