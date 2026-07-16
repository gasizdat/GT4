using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.Dialogs;

public partial class SelectFamilyDialog : ContentPage
{
  private readonly TaskCompletionSource<Name?> _Info = new(null);
  private readonly Name[] _Families;
  private Name? _CurrentFamily;

  public SelectFamilyDialog(Name[] families, IComparer<Name> nameComparer)
  {
    _Families = [.. families.OrderBy(family => family, nameComparer)];

    InitializeComponent();
  }

  public Name[] Families => _Families;

  public Name? CurrentFamily
  {
    get => _CurrentFamily;
    set
    {
      if (_CurrentFamily?.Id != value?.Id)
      {
        _CurrentFamily = value;

        OnPropertyChanged(nameof(CurrentFamily));
        OnPropertyChanged(nameof(DialogButtonName));
      }
    }
  }

  public string DialogButtonName => _CurrentFamily is null ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;

  public Task<Name?> Family => _Info.Task;

  private void OnSelectFamilyBtn(object sender, EventArgs e)
  {
    _Info.SetResult(_CurrentFamily);
  }
}
