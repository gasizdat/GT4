using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Items;
using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class SelectNameDialog : ContentPage
{
  public record NameTypeInfoItem(string NameTypeName, NameType Type);

  private readonly TaskCompletionSource<Name?> _Info = new(null);
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  public readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly NameType[] _NameTypes = [NameType.FirstName, NameType.MiddleName, NameType.LastName, NameType.AdditionalName];
  private readonly NameType _NameDeclension;
  private NameType _CurrentNameType = NameType.FirstName;
  private Name? _Name = null;
  private bool _NotReady => _Name is null;


  public SelectNameDialog(BiologicalSex biologicalSex, ServiceProvider serviceProvider)
  {
    _NameTypeFormatter = serviceProvider.GetRequiredService<INameTypeFormatter>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();

    InitializeComponent();

    switch (biologicalSex)
    {
      case BiologicalSex.Male:
        _NameDeclension = NameType.MaleDeclension;
        break;
      case BiologicalSex.Female:
        _NameDeclension = NameType.FemaleDeclension;
        break;
      default:
        _NameDeclension = NameType.AllNames;
        break;

    }
  }

  public ICollection<NameTypeInfoItem> NameTypes =>
    _NameTypes
    .Select(type => new NameTypeInfoItem(_NameTypeFormatter.ToString(type), type))
    .ToArray();

  public ICollection<NameInfoItem> Names =>
    _CurrentProjectProvider
    .Project
    .Names
    .GetNamesAsync(_CurrentNameType | _NameDeclension, _CancellationTokenProvider.CreateDbCancellationToken())
    .Result
    .Values
    .Select(name => new NameInfoItem(name, _NameTypeFormatter))
    .ToArray();

  public string SelectNameBtnName => _NotReady ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;
  public Task<Name?> Name => _Info.Task;

  public void OnSelectNameBtn(object sender, EventArgs e)
  {

    _Info.SetResult(_NotReady ? null : null);
  }
}