namespace GT4.UI.Utils;

public static class FilePickUtils
{
  public static byte[] FromStream(Stream stream)
  {
    using var ret = new MemoryStream();
    stream.CopyTo(ret);
    return ret.ToArray();
  }

  public static async Task<IEnumerable<FileResult>?> PickFilesAsync(PickOptions pickOptions, bool allowMultiple)
  {
    if (allowMultiple)
    {
      var files = await FilePicker.Default.PickMultipleAsync(pickOptions);
      return files?.Where(f => f is not null).Select(r => r!);
    }

    var result = await FilePicker.Default.PickAsync(pickOptions);
    return result is null ? null : [result];
  }
}
