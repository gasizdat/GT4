using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Converters;
using GT4.UI.Items;
using GT4.UI.Utils;
using Microsoft.Extensions.Http;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class AttachmentDataConverterTests
{
  private const int CacheSizeLimit = 1024 * 1024;

  private static AttachmentDataConverter Converter(DataCategory category) =>
    new(category, Mock.Of<IHttpClientFactory>(), new ImageCache(CacheSizeLimit));

  [Fact]
  public async Task ToObjectAsync_returns_null_for_a_null_attachment()
  {
    var result = await Converter(DataCategory.PersonAttachment).ToObjectAsync(null, CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task ToObjectAsync_returns_the_decoded_file_name()
  {
    var content = GedcomPhotoResidue.EncodeAttachment([1, 2, 3], "deed.pdf");
    var attachment = new Data(1, content, "application/pdf", DataCategory.PersonAttachment);

    var result = await Converter(DataCategory.PersonAttachment).ToObjectAsync(attachment, CancellationToken.None);

    var info = Assert.IsType<AttachmentInfo>(result);
    Assert.Equal("deed.pdf", info.FileName);
  }

  // An image attachment is embeddable in a biography as well as openable from the attachment list, so
  // the converter hands back the same renderable image a photo's would carry.
  [Fact]
  public async Task ToObjectAsync_renders_an_image_attachment_from_its_unwrapped_bytes()
  {
    var content = GedcomPhotoResidue.EncodeAttachment([9, 8, 7], "scan.jpg");
    var attachment = new Data(1, content, "image/jpeg", DataCategory.PersonAttachment);

    var result = await Converter(DataCategory.PersonAttachment).ToObjectAsync(attachment, CancellationToken.None);

    var info = Assert.IsType<AttachmentInfo>(result);
    Assert.NotNull(info.Image);
    Assert.Equal([9, 8, 7], await PhotoBytes.ReadAsync(info.Image));
  }

  [Fact]
  public async Task ToObjectAsync_leaves_a_non_image_attachment_without_an_image()
  {
    var content = GedcomPhotoResidue.EncodeAttachment([9, 8, 7], "deed.pdf");
    var attachment = new Data(1, content, "application/pdf", DataCategory.PersonAttachment);

    var result = await Converter(DataCategory.PersonAttachment).ToObjectAsync(attachment, CancellationToken.None);

    var info = Assert.IsType<AttachmentInfo>(result);
    Assert.Null(info.Image);
  }

  [Fact]
  public async Task FromObjectAsync_returns_null_for_input_that_is_not_an_AttachmentPick()
  {
    var result = await Converter(DataCategory.PersonAttachment).FromObjectAsync("not a pick", CancellationToken.None);

    Assert.Null(result);
  }

  [Fact]
  public async Task FromObjectAsync_encodes_the_picked_bytes_and_file_name()
  {
    var pick = new AttachmentPick([9, 8, 7], "scan.pdf", "application/pdf");

    var result = await Converter(DataCategory.PersonAttachment).FromObjectAsync(pick, CancellationToken.None);

    Assert.NotNull(result);
    Assert.Equal(DataCategory.PersonAttachment, result.Category);
    Assert.Equal("application/pdf", result.MimeType);
    Assert.Equal([9, 8, 7], GedcomPhotoResidue.ExtractImageBytes(result.Content));
    Assert.Equal("scan.pdf", await GedcomPhotoResidue.ExtractFileNameAsync(result, CancellationToken.None));
  }

  [Fact]
  public async Task FromObjectAsync_falls_back_to_octet_stream_when_the_picker_reports_no_content_type()
  {
    var pick = new AttachmentPick([1], "unknown.bin", null);

    var result = await Converter(DataCategory.PersonAttachment).FromObjectAsync(pick, CancellationToken.None);

    Assert.Equal("application/octet-stream", result?.MimeType);
  }

  [Fact]
  public async Task FromObjectAsync_stamps_the_category_it_was_constructed_with()
  {
    var pick = new AttachmentPick([9, 8, 7], "scan.pdf", "application/pdf");

    var result = await Converter(DataCategory.FamilyAttachment).FromObjectAsync(pick, CancellationToken.None);

    Assert.Equal(DataCategory.FamilyAttachment, result?.Category);
  }
}
