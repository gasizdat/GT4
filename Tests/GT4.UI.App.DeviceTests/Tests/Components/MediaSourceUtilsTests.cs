using GT4.Core.Project.Dto;
using GT4.UI.Converters;
using Xunit;

namespace GT4.UI.DeviceTests;

public class MediaSourceUtilsTests
{
  // Matches the [4-byte tag-length][UTF-8 GEDCOM tag text][image bytes] layout GedcomPhotoResidue
  // encodes; hand-built here since Encode itself isn't visible outside Core.Gedcom.
  private static byte[] BuildTaggedPhotoContent(string tagText, byte[] imageBytes)
  {
    var tagBytes = System.Text.Encoding.UTF8.GetBytes(tagText);
    using var buffer = new MemoryStream();
    buffer.Write(BitConverter.GetBytes(tagBytes.Length));
    buffer.Write(tagBytes);
    buffer.Write(imageBytes);
    return buffer.ToArray();
  }

  [Fact]
  public void PlainPhoto_EncodesItsRawBytes()
  {
    var photo = new Data(1, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var sources = MediaSourceUtils.BuildMediaSources([photo]);

    Assert.Equal($"data:image/png;base64,{Convert.ToBase64String([1, 2, 3])}", sources[1]);
  }

  [Fact]
  public void TaggedPhoto_StripsTheResidueEnvelopeBeforeEncoding()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 TITL A caption\n", [4, 5, 6]);
    var photo = new Data(2, content, "image/jpeg", DataCategory.PersonMainPhotoTagged);

    var sources = MediaSourceUtils.BuildMediaSources([photo]);

    Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String([4, 5, 6])}", sources[2]);
  }

  [Fact]
  public void ImageAttachment_IsInlinedLikeAPhoto()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.jpg\n", [7, 8, 9]);
    var attachment = new Data(3, content, "image/jpeg", DataCategory.PersonAttachment);

    var sources = MediaSourceUtils.BuildMediaSources([attachment]);

    Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String([7, 8, 9])}", sources[3]);
  }

  [Fact]
  public void NonImageAttachment_IsExcluded()
  {
    var content = BuildTaggedPhotoContent("0 OBJE\n1 FILE scan.pdf\n", [7, 8, 9]);
    var attachment = new Data(4, content, "application/pdf", DataCategory.PersonAttachment);

    var sources = MediaSourceUtils.BuildMediaSources([attachment]);

    Assert.Empty(sources);
  }

  // A GEDCOM OBJE with no FORM imports as a photo with no MIME type; it stays inlinable, so the data
  // URI declares no media type and the rendering WebView is left to sniff the bytes.
  [Fact]
  public void PhotoWithoutAMimeType_IsStillInlined()
  {
    var photo = new Data(5, [1, 2, 3], null, DataCategory.PersonPhoto);

    var sources = MediaSourceUtils.BuildMediaSources([photo]);

    Assert.Equal($"data:;base64,{Convert.ToBase64String([1, 2, 3])}", sources[5]);
  }

  [Fact]
  public void NotYetSavedPhoto_IsExcluded()
  {
    var photo = new Data(ElementId.NonCommittedId, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);

    var sources = MediaSourceUtils.BuildMediaSources([photo]);

    Assert.Empty(sources);
  }
}
