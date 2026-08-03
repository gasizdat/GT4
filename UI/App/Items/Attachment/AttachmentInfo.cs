using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Converters;

namespace GT4.UI.Items;

public sealed record AttachmentInfo(string FileName, string? Title, Data Data)
{
  public static async Task<AttachmentInfo> CreateAsync(Data data, CancellationToken token)
  {
    var fileName = await GedcomPhotoResidue.ExtractFileNameAsync(data, token);
    var title = await GedcomPhotoResidue.ExtractTitleAsync(data, token);

    return new AttachmentInfo(fileName ?? string.Empty, title, data);
  }

  public string DisplayName => string.IsNullOrWhiteSpace(Title) ? FileName : Title;

  public bool ShowFileName => !string.IsNullOrWhiteSpace(Title);

  public byte[] Bytes => MediaSourceUtils.PayloadBytes(Data);
}
