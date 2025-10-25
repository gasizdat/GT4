using GT4.UI.Resources;

namespace GT4.UI.App.Dialogs;

public partial class CreateNewFamilyDialog : ContentPage
{
  public record FamilyInfo(string Name, string MaleLastName, string FemaleLastName);

  private TaskCompletionSource<FamilyInfo?> _Info = new();
  private string _FamilyName = string.Empty;
  private string _MaleLastName = string.Empty;
  private string _FemaleLastName = string.Empty;

  public CreateNewFamilyDialog()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public string FamilyName
  {
    get => _FamilyName; 
    set
    {
      _FamilyName = value;
      OnPropertyChanged(nameof(FamilyName));
      OnPropertyChanged(nameof(MaleLastName));
      OnPropertyChanged(nameof(FemaleLastName));
      OnPropertyChanged(nameof(CreateFamilyBtnName));
    }
  }
  public string MaleLastName { get => _MaleLastName; set => _MaleLastName = value; }
  public string FemaleLastName { get => _FemaleLastName; set => _FemaleLastName = value; }
  public Task<FamilyInfo?> Info => _Info.Task;
  public string CreateFamilyBtnName => string.IsNullOrWhiteSpace(FamilyName) ? UIStrings.BtnNameCancel : UIStrings.BtnNameCreateFamily;

  public void OnCreateFamilyBtn(object sender, EventArgs e)
  {
    _Info.SetResult(new(FamilyName, MaleLastName, FemaleLastName));
  }
}