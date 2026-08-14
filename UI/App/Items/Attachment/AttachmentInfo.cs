using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Items;

// Image is set only for an attachment whose own MIME type says it renders as one: an attached scan is
// embeddable in a biography just like a photo, and it stays in the attachment list either way.
public sealed record AttachmentInfo(string FileName, string? Title, Data Data, PhotoInfo? Image = null)
{
  public string DisplayName => string.IsNullOrWhiteSpace(Title) ? FileName : Title;

  public bool ShowFileName => !string.IsNullOrWhiteSpace(Title);

  // FileName is often a full original path -- a common artifact of GEDCOM imports.
  public async Task OpenAsync(CancellationToken token)
  {
    var fileName = FileNameUtils.Sanitize(FileName, "attachment");
    var path = Path.Combine(FileSystem.CacheDirectory, fileName);
    var bytes = GedcomPhotoResidue.ExtractImageBytes(Data.Content);
    await File.WriteAllBytesAsync(path, bytes, token);

    await Launcher.Default.OpenAsync(new OpenFileRequest(fileName, new ReadOnlyFile(path)));
  }
}
