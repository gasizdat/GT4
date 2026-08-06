using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class PhotoCacheTests
{
  private sealed class CountingImageConverter : IDataConverter
  {
    public int CallCount { get; private set; }

    public Task<Data?> FromObjectAsync(object? data, CancellationToken token) => Task.FromResult<Data?>(null);

    public Task<object?> ToObjectAsync(Data? data, CancellationToken token)
    {
      CallCount++;
      return Task.FromResult<object?>(data is null ? null : new PhotoInfo(ImageUtils.ImageFromBytes(data.Content), null));
    }
  }

  private static async Task<Data> PhotoDataAsync(int id) =>
    new(id, (await ImageUtils.ToBytesAsync("male_stub.png", CancellationToken.None))!, "image/png", DataCategory.PersonMainPhoto);

  private static Mock<ICurrentProjectProvider> CurrentProjectProvider(ProjectInfo info)
  {
    var provider = new Mock<ICurrentProjectProvider>();
    provider.SetupGet(p => p.Info).Returns(info);
    return provider;
  }

  [Fact]
  public async Task GetOrResolveAsync_resolves_once_and_caches_by_DataId()
  {
    var cache = new PhotoCache(CurrentProjectProvider(TestServices.SampleProjectInfo).Object);
    var converter = new CountingImageConverter();
    var data = await PhotoDataAsync(1);
    var fallback = ImageUtils.ImageFromRawResource("male_stub.png");

    var first = await cache.GetOrResolveAsync(_ => converter, data, fallback, 64);
    var second = await cache.GetOrResolveAsync(_ => converter, data, fallback, 64);

    Assert.Equal(1, converter.CallCount);
    Assert.Same(first, second);
  }

  [Fact]
  public async Task GetOrResolveAsync_caches_the_downsized_photo()
  {
    var cache = new PhotoCache(CurrentProjectProvider(TestServices.SampleProjectInfo).Object);
    var converter = new CountingImageConverter();
    var data = await PhotoDataAsync(1);
    var fallback = ImageUtils.ImageFromRawResource("male_stub.png");

    var photo = await cache.GetOrResolveAsync(_ => converter, data, fallback, 64);

    using var stream = await ((StreamImageSource)photo.Source).Stream(CancellationToken.None);
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);
    var size = ImageUtils.PixelSize(buffer.ToArray())!.Value;
    Assert.True(Math.Max(size.Width, size.Height) <= 64);
  }

  [Fact]
  public async Task GetOrResolveAsync_reresolves_after_the_open_project_changes()
  {
    var provider = CurrentProjectProvider(TestServices.SampleProjectInfo);
    var cache = new PhotoCache(provider.Object);
    var converter = new CountingImageConverter();
    var data = await PhotoDataAsync(1);
    var fallback = ImageUtils.ImageFromRawResource("male_stub.png");

    await cache.GetOrResolveAsync(_ => converter, data, fallback, 64);
    provider.SetupGet(p => p.Info).Returns(TestServices.SampleProjectInfo with { Revision = 1 });
    await cache.GetOrResolveAsync(_ => converter, data, fallback, 64);

    Assert.Equal(2, converter.CallCount);
  }

  [Fact]
  public async Task GetOrResolveAsync_does_not_cache_when_no_converter_is_registered()
  {
    var cache = new PhotoCache(CurrentProjectProvider(TestServices.SampleProjectInfo).Object);
    var data = await PhotoDataAsync(1);
    var fallback = ImageUtils.ImageFromRawResource("male_stub.png");
    var resolverCallCount = 0;
    OptionalDataConverterResolver resolver = _ => { resolverCallCount++; return null; };

    var first = await cache.GetOrResolveAsync(resolver, data, fallback, 64);
    await cache.GetOrResolveAsync(resolver, data, fallback, 64);

    Assert.Same(fallback, first.Source);
    Assert.Equal(2, resolverCallCount);
  }

  [Fact]
  public async Task GetOrResolveAsync_does_not_cache_a_photo_that_fails_to_downsize()
  {
    var cache = new PhotoCache(CurrentProjectProvider(TestServices.SampleProjectInfo).Object);
    var converter = new CountingImageConverter();
    var data = new Data(1, [1, 2, 3], "image/png", DataCategory.PersonMainPhoto);
    var fallback = ImageUtils.ImageFromRawResource("male_stub.png");

    await cache.GetOrResolveAsync(_ => converter, data, fallback, 64);
    await cache.GetOrResolveAsync(_ => converter, data, fallback, 64);

    Assert.Equal(2, converter.CallCount);
  }

  [Fact]
  public async Task GetOrResolveAsync_clears_the_whole_cache_once_the_entry_cap_is_reached()
  {
    var cache = new PhotoCache(CurrentProjectProvider(TestServices.SampleProjectInfo).Object);
    var converter = new CountingImageConverter();
    var fallback = ImageUtils.ImageFromRawResource("male_stub.png");

    for (var id = 1; id <= 65; id++)
    {
      await cache.GetOrResolveAsync(_ => converter, await PhotoDataAsync(id), fallback, 64);
    }
    Assert.Equal(65, converter.CallCount);

    // The 65th insert crossed the cap and cleared everything first, so entry 1 is gone but the
    // just-inserted entry 65 survives.
    await cache.GetOrResolveAsync(_ => converter, await PhotoDataAsync(1), fallback, 64);
    Assert.Equal(66, converter.CallCount);

    await cache.GetOrResolveAsync(_ => converter, await PhotoDataAsync(65), fallback, 64);
    Assert.Equal(66, converter.CallCount);
  }
}
