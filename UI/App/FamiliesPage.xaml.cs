using GT4.Core.Project;
using System.Collections.ObjectModel;

namespace GT4.UI;

using GT4.UI.Resources;

public partial class FamiliesPage : ContentPage
{
  private readonly ServiceProvider _services = ServiceBuilder.DefaultServices;

  public FamiliesPage()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public ICollection<FamilyInfoItem> Families
  {
    get
    {
      using var token = new Core.Utils.DefaultCancellationToken();
      var ret = _services.GetRequiredService<ICurrentProjectProvider>()
        .Project
        .Names
        .GetNamesAsync(Core.Project.Dto.NameType.FamilyName, token)
        .Result
        .Select(name => new FamilyInfoItem(name.Value))
        .ToList();

      ret.Add(new FamilyInfoItemCreate());

      return ret;
    }
  }

  public async void OnFamilySelected(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection.FirstOrDefault() is ProjectItemCreate item)
    {
      await OnCreateFamily();
    }
  }

  public async void OnDeleteFamilySelected(object sender, EventArgs e)
  {

    var item = (sender as BindableObject)?.BindingContext as FamilyInfoItem;
    if (item is null or FamilyInfoItemCreate)
      return;

    try
    {
      var result = await DisplayAlert(UIStrings.AlertTitleConfirmation,
        string.Format(UIStrings.AlertTextDeleteConfirmationText, item.FamilyName), UIStrings.BtnNameYes, UIStrings.BtnNameNo);

      if (result == false)
        return;

      using var token = new Core.Utils.DefaultCancellationToken();
      // TODO
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
    finally
    {
      OnPropertyChanged(nameof(Families));
    }
  }

  internal async Task OnCreateFamily()
  {
    var dialog = new CreateNewProjectDialog();

    await Navigation.PushModalAsync(dialog);
    var projectInfo = await dialog.ProjectInfo;
    await Navigation.PopModalAsync();

    try
    {
      if (projectInfo.Name == string.Empty)
        return;

      using var token = new Core.Utils.DefaultCancellationToken();
      await _services.GetRequiredService<IProjectList>().CreateAsync(projectInfo, token);
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
    finally
    {
      OnPropertyChanged(nameof(Families));
    }
  }
}