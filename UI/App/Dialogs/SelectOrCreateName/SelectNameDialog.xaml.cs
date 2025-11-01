using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Items;
using GT4.UI.Resources;
using System.Collections.ObjectModel;

namespace GT4.UI.App.Dialogs;

public partial class SelectNameDialog : ContentPage
{
  public record NameTypeInfoItem(string TypeName, NameType Type);

  private readonly TaskCompletionSource<Name?> _Info = new(null);
  private readonly INameTypeFormatter _NameTypeFormatter;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ObservableCollection<NameTypeInfoItem> _NameTypes;
  private NameTypeInfoItem _CurrentNameType;
  private readonly NameType _NameDeclension;
  private NameInfoItem? _CurrentName;

  private bool _NotReady => _CurrentName is null;


  public SelectNameDialog(BiologicalSex biologicalSex, ServiceProvider serviceProvider)
  {
    _NameTypeFormatter = serviceProvider.GetRequiredService<INameTypeFormatter>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _NameTypes = new((new[] { NameType.FirstName, NameType.MiddleName, NameType.LastName, NameType.AdditionalName })
      .Select(type => new NameTypeInfoItem(_NameTypeFormatter.ToString(type), type)));
    _CurrentNameType = _NameTypes.First();

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

  public ICollection<NameTypeInfoItem> NameTypes => _NameTypes;

  public ICollection<NameInfoItem> Names =>
    _CurrentProjectProvider
    .Project
    .Names
    .GetNamesAsync(CurrentNameType.Type | _NameDeclension, _CancellationTokenProvider.CreateDbCancellationToken())
    .Result
    .Values
    .Select(name => new NameInfoItem(name, _NameTypeFormatter))
    .ToArray();

  public string SelectNameBtnName => _NotReady ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;

  public Task<Name?> Name => _Info.Task;

  public NameTypeInfoItem CurrentNameType
  {
    get => _CurrentNameType;
    set
    {
      if (_CurrentNameType == value)
        return;

      _CurrentNameType = value;
      OnPropertyChanged(nameof(Names));
    }
  }

  public NameInfoItem? CurrentName
  {
    get => _CurrentName;
    set
    {
      _CurrentName = value;
      OnPropertyChanged(nameof(SelectNameBtnName));
    }
  }

  public void OnSelectNameBtn(object sender, EventArgs e)
  {
    _Info.SetResult(_NotReady ? null : _CurrentName?.Info);
  }
}