using System.Text;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;

namespace GT4.Core.Gedcom;

/// <summary>
/// Stores a photo's own residual GEDCOM tags (chiefly <c>TITL</c>, the caption) alongside its raw
/// bytes, for photos whose <see cref="DataCategory"/> is <see cref="DataCategory.PersonMainPhotoTagged"/>/
/// <see cref="DataCategory.PersonPhotoTagged"/>. Layout: <c>[4-byte tag-length][UTF-8
/// GedcomWriter-serialized residual node][raw photo bytes]</c>. There is no magic-byte marker to sniff --
/// <see cref="DataCategoryExtensions.IsTaggedPhoto"/> on the category alone decides whether a photo's
/// <c>Content</c> uses this layout at all, so the shape is never ambiguous.
/// </summary>
public static class GedcomPhotoResidue
{
  internal static byte[] Encode(byte[] imageBytes, GedcomNode residual)
  {
    var tagWriter = new StringWriter();
    GedcomWriter.Write(tagWriter, residual);
    var tagBytes = Encoding.UTF8.GetBytes(tagWriter.ToString());

    using var buffer = new MemoryStream(sizeof(int) + tagBytes.Length + imageBytes.Length);
    buffer.Write(BitConverter.GetBytes(tagBytes.Length));
    buffer.Write(tagBytes);
    buffer.Write(imageBytes);
    return buffer.ToArray();
  }

  // Used by GedcomExporter, which needs the full node to re-attach its children onto a regenerated OBJE.
  internal static async Task<(byte[] ImageBytes, GedcomNode Residual)> DecodeAsync(byte[] content, CancellationToken token)
  {
    var tagLength = BitConverter.ToInt32(content, 0);
    var tagText = Encoding.UTF8.GetString(content, sizeof(int), tagLength);
    var imageBytes = content[(sizeof(int) + tagLength)..];

    var roots = await GedcomReader.ReadAsync(new StringReader(tagText), token);
    return (imageBytes, roots[0]);
  }

  /// <summary>Synchronous: just reads the length prefix and slices the remainder -- no GEDCOM parse at all.</summary>
  public static byte[] ExtractImageBytes(byte[] content)
  {
    var tagLength = BitConverter.ToInt32(content, 0);
    return content[(sizeof(int) + tagLength)..];
  }

  public static async Task<string?> ExtractTitleAsync(Data? photo, CancellationToken token)
  {
    if (photo is null || !photo.Category.IsTaggedPhoto())
      return null;

    var (_, residual) = await DecodeAsync(photo.Content, token);
    return residual.ChildValue(GedcomTags.Title);
  }
}
