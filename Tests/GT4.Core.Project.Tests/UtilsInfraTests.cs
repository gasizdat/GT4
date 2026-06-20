using FluentAssertions;
using GT4.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GT4.Core.Project.Tests;

using IFileSystem = GT4.Core.Utils.IFileSystem;
using CoreFileSystem = GT4.Core.Utils.FileSystem;

/// <summary>
/// Coverage for the Core.Utils infrastructure: cancellation-token helpers, <see cref="Storage"/>,
/// the interactive <see cref="AppConfigurationProvider"/>, the DI registration extensions and the
/// real-disk <see cref="FileSystem"/>.
/// </summary>
public sealed class UtilsInfraTests
{
  // --- Cancellation ------------------------------------------------------------------------------

  [Fact]
  public void CancellationTokenHost_ExposesAndImplicitlyConvertsToken()
  {
    using var host = new CancellationTokenHost(TimeSpan.FromSeconds(30));

    host.Token.IsCancellationRequested.Should().BeFalse();
    CancellationToken token = host; // implicit conversion
    token.Should().Be(host.Token);
  }

  [Fact]
  public void CancellationTokenProvider_CreatesUsableHosts()
  {
    var provider = new CancellationTokenProvider();

    using var db = provider.CreateDbCancellationToken();
    using var shortOp = provider.CreateShortOperationCancellationToken();

    db.Token.IsCancellationRequested.Should().BeFalse();
    shortOp.Token.IsCancellationRequested.Should().BeFalse();
  }

  // --- Storage -----------------------------------------------------------------------------------

  [Fact]
  public void Storage_ExposesDistinctWellKnownFolders()
  {
    var storage = new Storage();

    storage.ProjectsRoot.Root.Should().Be(Environment.SpecialFolder.MyDocuments);
    storage.ProjectsCache.Path.Should().Contain(".cache");
    storage.AppConfig.Path.Should().Contain(".config");
    storage.ProjectsCache.Should().NotBe(storage.AppConfig);
  }

  // --- AppConfigurationProvider ------------------------------------------------------------------

  private static (AppConfigurationProvider provider, string root) NewConfigProvider()
  {
    var root = Path.Combine(Path.GetTempPath(), $"gt4_cfg_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    var provider = new AppConfigurationProvider(new DiskFileSystem(root), new TempStorage());
    return (provider, root);
  }

  [Fact]
  public void AppConfiguration_Load_WithNoFile_LeavesProviderEmpty()
  {
    var (provider, root) = NewConfigProvider();
    try
    {
      provider.Load();

      provider.TryGet("missing", out _).Should().BeFalse();
      provider.Name.Should().Be(WellKnownActiveConfigurations.AppConfig);
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void AppConfiguration_SetKey_PersistsAndReloads()
  {
    var (provider, root) = NewConfigProvider();
    try
    {
      provider.SetKey("theme", "dark");
      provider.TryGet("theme", out var live).Should().BeTrue();
      live.Should().Be("dark");
      provider.Flush();

      // A fresh provider over the same files reads the persisted value back.
      var reopened = new AppConfigurationProvider(new DiskFileSystem(root), new TempStorage());
      reopened.Load();
      reopened.TryGet("theme", out var persisted).Should().BeTrue();
      persisted.Should().Be("dark");
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void AppConfiguration_RemoveKey_DeletesIt()
  {
    var (provider, root) = NewConfigProvider();
    try
    {
      provider.SetKey("temp", "x");
      provider.RemoveKey("temp");

      provider.TryGet("temp", out _).Should().BeFalse();

      var reopened = new AppConfigurationProvider(new DiskFileSystem(root), new TempStorage());
      reopened.Load();
      reopened.TryGet("temp", out _).Should().BeFalse();
    }
    finally { Directory.Delete(root, true); }
  }

  // --- DI extensions -----------------------------------------------------------------------------

  [Fact]
  public void AddCoreUtils_RegistersStorageAndCancellationProvider()
  {
    using var sp = new ServiceCollection().AddCoreUtils().BuildServiceProvider();

    sp.GetService<IStorage>().Should().BeOfType<Storage>();
    sp.GetService<ICancellationTokenProvider>().Should().BeOfType<CancellationTokenProvider>();
    sp.GetService<IFileSystem>().Should().BeOfType<CoreFileSystem>();
  }

  [Fact]
  public void AddActiveConfigurations_RegistersInteractiveProvidersByName()
  {
    var root = Path.Combine(Path.GetTempPath(), $"gt4_cfg_{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
      var provider = new AppConfigurationProvider(new DiskFileSystem(root), new TempStorage());
      var configurationRoot = new ConfigurationRoot(new IConfigurationProvider[] { provider });

      using var sp = new ServiceCollection().AddActiveConfigurations(configurationRoot).BuildServiceProvider();

      var resolved = sp.GetKeyedService<IInteractiveConfiguration>(WellKnownActiveConfigurations.AppConfig);
      resolved.Should().BeSameAs(provider);
    }
    finally { Directory.Delete(root, true); }
  }

  [Fact]
  public void AddAppConfiguration_BuildsConfigurationWithInteractiveProvider()
  {
    var builder = new ConfigurationBuilder().AddAppConfiguration();

    var configurationRoot = builder.Build();

    configurationRoot.Providers.OfType<IInteractiveConfiguration>().Should().ContainSingle();
  }

  // --- FileSystem (real disk under a temp special-folder subdir) ----------------------------------

  [Fact]
  public void FileSystem_FileLifecycle_WriteReadCopyListRemove()
  {
    var fs = new CoreFileSystem();
    var dir = new DirectoryDescription(Environment.SpecialFolder.LocalApplicationData, ["GT4_fs_test", Guid.NewGuid().ToString("N")]);
    var file = new FileDescription(dir, "a.txt", null);
    var copy = new FileDescription(dir, "b.txt", null);

    try
    {
      fs.FileExists(file).Should().BeFalse();
      fs.GetFiles(dir, "*.txt", recursive: false).Should().BeEmpty();

      var payload = new byte[] { 1, 2, 3, 4 };
      using (var write = fs.OpenWriteStream(file))
      {
        write.Write(payload, 0, payload.Length);
      }
      fs.FileExists(file).Should().BeTrue();

      using (var read = fs.OpenReadStream(file))
      using (var ms = new MemoryStream())
      {
        read.CopyTo(ms);
        ms.ToArray().Should().Equal(payload);
      }

      fs.Copy(file, copy);
      fs.FileExists(copy).Should().BeTrue();

      var listed = fs.GetFiles(dir, "*.txt", recursive: false);
      listed.Select(f => f.FileName).Should().BeEquivalentTo(["a.txt", "b.txt"]);
      // The reconstructed description round-trips through the filesystem.
      fs.FileExists(listed.First()).Should().BeTrue();

      fs.GetLastWriteTime(file).Should().BeOnOrBefore(DateTime.Now.AddMinutes(1));

      fs.RemoveFile(file);
      fs.FileExists(file).Should().BeFalse();
      // Removing a missing file is a no-op.
      fs.RemoveFile(file);
    }
    finally
    {
      fs.RemoveDirectory(dir);
      fs.RemoveDirectory(dir); // second call is a no-op (directory already gone).
    }
  }

  [Fact]
  public void FileSystem_Copy_FromStream_AndRecursiveListing()
  {
    var fs = new CoreFileSystem();
    var dir = new DirectoryDescription(Environment.SpecialFolder.LocalApplicationData, ["GT4_fs_test", Guid.NewGuid().ToString("N")]);
    var nested = dir with { Path = [.. dir.Path, "sub"] };
    var top = new FileDescription(dir, "top.dat", null);
    var deep = new FileDescription(nested, "deep.dat", null);

    try
    {
      using (var source = new MemoryStream(new byte[] { 9, 8, 7 }))
      {
        fs.Copy(source, top);
      }
      using (var write = fs.OpenWriteStream(deep)) { write.WriteByte(42); }

      fs.GetFiles(dir, "*.dat", recursive: false).Should().ContainSingle();
      fs.GetFiles(dir, "*.dat", recursive: true).Should().HaveCount(2);
    }
    finally
    {
      fs.RemoveDirectory(dir);
    }
  }
}
