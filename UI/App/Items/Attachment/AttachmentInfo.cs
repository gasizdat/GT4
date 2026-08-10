using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;

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

  public byte[] Bytes => GedcomPhotoResidue.PayloadBytes(Data);

  // FileName is often a full original path -- a common artifact of GEDCOM imports.
  public async Task OpenAsync(CancellationToken token)
  {
    var fileName = FileNameUtils.Sanitize(FileName, "attachment");
    var path = Path.Combine(FileSystem.CacheDirectory, fileName);
    await File.WriteAllBytesAsync(path, Bytes, token);

    await Launcher.Default.OpenAsync(new OpenFileRequest(fileName, new ReadOnlyFile(path)));
  }
}
