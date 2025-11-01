using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Dialogs;
using GT4.UI.App.Items;
using GT4.UI.Resources;

namespace GT4.UI.App.Pages;

public partial class FamiliesPage : ContentPage
{
  private PersonInfoItem[] GetFamilyPersons(Name name, CancellationToken token)
  {
    var nameFormatter = Services.GetRequiredService<INameFormatter>();
    return Services.GetRequiredService<ICurrentProjectProvider>()
      .Project
      .Persons
      .GetPersonsByNameAsync(name, token)
      .Result
      .Select(person => new PersonInfoItem(person, nameFormatter))
      .OrderBy(item => item, Services.GetRequiredService<IComparer<PersonInfoItem>>())
      .ToArray();
  }

  public FamiliesPage()
  {
    InitializeComponent();
    BindingContext = this;
  }

  public ServiceProvider Services { get; set; } = ServiceBuilder.DefaultServices;

  public ICollection<FamilyInfoItem> Families
  {
    get
    {
      try
      {
        using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
        var ret = Services.GetRequiredService<ICurrentProjectProvider>()
          .Project
          .Names
          .GetNamesAsync(NameType.FamilyName, token)
          .Result
          .Values
          .Select(name => new FamilyInfoItem(name, GetFamilyPersons(name, token)))
          .OrderBy(item => item, Services.GetRequiredService<IComparer<FamilyInfoItem>>())
          .ToList();

        ret.Add(new FamilyInfoItemCreate());

        return ret;
      }
      catch (Exception ex)
      {
        return [new FamilyInfoItemRefresh(ex)];
      }
    }
  }

  public async void OnFamilySelected(object sender, SelectionChangedEventArgs e)
  {
    switch (e.CurrentSelection.FirstOrDefault())
    {
      case FamilyInfoItemCreate:
        await OnCreateFamily();
        break;
      case FamilyInfoItemRefresh:
        OnPropertyChanged(nameof(Families));
        break;
      case FamilyInfoItem item:
        await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyPage>(), true, new() { { "FamilyName", item.Info } });
        // TODO not so good approach
        if (sender is SelectableItemsView view)
        {
          view.SelectedItem = null;
        }
        break;
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
        string.Format(UIStrings.AlertTextDeleteConfirmationText_1, item.Info.Value), UIStrings.BtnNameYes, UIStrings.BtnNameNo);

      if (result == false)
        return;

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      await Services.GetRequiredService<ICurrentProjectProvider>()
        .Project
        .Family
        .RemoveFamilyAsync(item.Info, token);
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
    var dialog = new CreateNewNameDialog(NameType.FamilyName);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
      if (info is null)
      {
        return;
      }

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      var family = await Services.GetRequiredService<ICurrentProjectProvider>()
        .Project
        .Family
        .AddFamilyAsync(familyName: info.Name, maleLastName: info.MaleLastName, femaleLastName: info.FemaleLastName, token);
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