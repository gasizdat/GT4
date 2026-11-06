using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Resources;
using GT4.UI.Utils.Converters;

namespace GT4.UI.DeviceTests;

internal sealed class TestablePhotoEnhancer : PhotoEnhancer
{
  public TestablePhotoEnhancer(TestServices services, IPhotoEnhancement[] enhancements)
    : base(
      enhancements,
      services.ModelDownloader.Object,
      services.AlertService.Object,
      services.Provider.GetRequiredService<DataConverterResolver>())
  {
  }

  public string[] OfferedEnhancementNames { get; private set; } = [];

  public string EnhancementAnswer { get; set; } = UIStrings.BtnNameCancel;

  protected override Task<string> ChooseEnhancementAsync(Page page, string[] names)
  {
    OfferedEnhancementNames = names;
    return Task.FromResult(EnhancementAnswer);
  }
}
