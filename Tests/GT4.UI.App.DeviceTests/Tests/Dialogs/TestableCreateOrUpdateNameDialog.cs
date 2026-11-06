using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Dialogs;
using GT4.UI.Resources;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.DeviceTests;

internal sealed class TestableCreateOrUpdateNameDialog : CreateOrUpdateNameDialog
{
  public TestableCreateOrUpdateNameDialog(
    Name name,
    Name? maleName,
    Name? femaleName,
    INameTypeFormatter nameTypeFormatter,
    IAlertService alertService,
    ICancellationTokenProvider cancellationTokenProvider,
    DataConverterResolver dataConverterResolver,
    PhotoEnhancer photoEnhancer,
    FamilyFullInfo? family)
    : base(name, maleName, femaleName, nameTypeFormatter, alertService, cancellationTokenProvider, dataConverterResolver, photoEnhancer, family)
  {
  }

  public string EnhancementAnswer { get; set; } = UIStrings.BtnNameCancel;

  protected override Task<string> ChooseEnhancementAsync(string[] names) => Task.FromResult(EnhancementAnswer);
}
