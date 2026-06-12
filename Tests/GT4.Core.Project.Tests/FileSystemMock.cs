using GT4.Core.Utils;
using System.Text.RegularExpressions;

namespace GT4.Core.Project.Tests;

using IFileSystem = GT4.Core.Utils.IFileSystem;

/// <summary>
/// In-memory <see cref="IFileSystem"/> for <see cref="ProjectHost"/> tests: tracks files with their
/// last-write times, records successful copy/remove operations for ordering assertions, and can
/// inject copy failures to exercise the dispose retry loop.
/// </summary>
internal sealed class FileSystemMock : IFileSystem
{
  private readonly Dictionary<FileDescription, DateTime> _Files = new();

  /// <summary>Successful mutating operations, in order ("copy a -> b", "remove a").</summary>
  public List<string> Operations { get; } = new();

  /// <summary>Every <see cref="Copy(FileDescription, FileDescription)"/> call, including failed ones.</summary>
  public int CopyAttempts { get; private set; }

  /// <summary>While positive, each copy attempt throws and decrements the counter.</summary>
  public int CopyFailuresRemaining { get; set; }

  public void AddFile(FileDescription file, DateTime? lastWrite = null) =>
    _Files[file] = lastWrite ?? DateTime.UtcNow;

  public void ResetStats()
  {
    Operations.Clear();
    CopyAttempts = 0;
  }

  public bool FileExists(FileDescription FileExists) => _Files.ContainsKey(FileExists);

  public void Copy(FileDescription from, FileDescription to)
  {
    CopyAttempts++;
    if (CopyFailuresRemaining > 0)
    {
      CopyFailuresRemaining--;
      throw new IOException("Injected copy failure.");
    }
    if (!_Files.TryGetValue(from, out _))
    {
      throw new FileNotFoundException(ToPath(from));
    }
    _Files[to] = DateTime.UtcNow;
    Operations.Add($"copy {ToPath(from)} -> {ToPath(to)}");
  }

  public void RemoveFile(FileDescription fileDescription)
  {
    _Files.Remove(fileDescription);
    Operations.Add($"remove {ToPath(fileDescription)}");
  }

  public FileDescription[] GetFiles(DirectoryDescription directory, string searchPattern, bool recursive)
  {
    var regex = new Regex(
      "^" + Regex.Escape(searchPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
      RegexOptions.IgnoreCase);
    return _Files.Keys
      .Where(f => f.Directory == directory && regex.IsMatch(f.FileName))
      .ToArray();
  }

  public DateTime GetLastWriteTime(FileDescription fileDescription) => _Files[fileDescription];

  public string ToPath(DirectoryDescription fileDescription) =>
    string.Join('/', new[] { fileDescription.Root.ToString() }.Concat(fileDescription.Path));

  public string ToPath(FileDescription fileDescription) =>
    $"{ToPath(fileDescription.Directory)}/{fileDescription.FileName}";

  // Not used by ProjectHost.
  public Stream OpenWriteStream(FileDescription fileDescription) => throw new NotSupportedException();
  public Stream OpenReadStream(FileDescription fileDescription) => throw new NotSupportedException();
  public void Copy(Stream from, FileDescription to) => throw new NotSupportedException();
  public void RemoveDirectory(DirectoryDescription directory) => throw new NotSupportedException();
}
