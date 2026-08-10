namespace GT4.UI.Utils;

public static class FilePickUtils
{
  public readonly record struct PickedFile(byte[] Content, string FileName, string? MimeType);

  public static async Task<PickedFile[]?> PickFilesAsync(PickOptions pickOptions, bool allowMultiple)
  {
    if (allowMultiple)
    {
      var files = await FilePicker.Default.PickMultipleAsync(pickOptions);
      if (files is null)
      {
        return null;
      }

      return await Task.WhenAll(files.Where(f => f is not null).Select(f => ReadAsync(f!)));
    }

    var result = await FilePicker.Default.PickAsync(pickOptions);
    return result is null ? null : [await ReadAsync(result)];
  }

  private static async Task<PickedFile> ReadAsync(FileResult file)
  {
    using var stream = await file.OpenReadAsync();
    using var content = new MemoryStream();
    await stream.CopyToAsync(content);
    return new PickedFile(content.ToArray(), file.FileName, file.ContentType);
  }
}
