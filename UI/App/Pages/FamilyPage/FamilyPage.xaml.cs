using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Dialogs;
using GT4.UI.App.Items;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.App.Pages;

[QueryProperty(nameof(FamilyName), "FamilyName")]
public partial class FamilyPage : ContentPage
{
  private Name? _FamilyName = null;
  private int _PersonItemMinimalWidth;

  public FamilyPage()
  {
    InitializeComponent();
    BindingContext = this;
    MemberItemTappedCommand = new Command<FamilyMemberInfoItem>(OnMemberSelected);
  }

  public Name? FamilyName
  {
    get => _FamilyName;
    set
    {
      _FamilyName = value;
      OnPropertyChanged(nameof(Members));
      OnPropertyChanged(nameof(FamilyName));
    }
  }

  public ServiceProvider Services { get; set; } = ServiceBuilder.DefaultServices;

  public int PersonItemMinimalWidth => _PersonItemMinimalWidth;

  public ICommand MemberItemTappedCommand { get; init;  }

  public ICollection<FamilyMemberInfoItem> Members
  {
    get
    {
      if (FamilyName is null)
      {
        return [];
      }

      try
      {
        using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
        var ret = Services.GetRequiredService<ICurrentProjectProvider>()
          .Project
          .Persons
          .GetPersonsByNameAsync(FamilyName, token)
          .Result
          .Select(person => new FamilyMemberInfoItem(person, Services))
          .OrderBy(item => item, Services.GetRequiredService<IComparer<FamilyMemberInfoItem>>())
          .ToList();

        ret.Add(new FamilyMemberInfoItemCreate());

        return ret;
      }
      catch (Exception ex)
      {
        return [new FamilyMemberInfoItemRefresh(ex)];
      }
    }
  }

  internal async void OnMemberSelected(FamilyMemberInfoItem member)
  {
    switch (member)
    {
      case FamilyMemberInfoItemRefresh:
        OnPropertyChanged(nameof(Members));
        break;
      case FamilyMemberInfoItemCreate:
        await OnCreatePerson();
        break;
      case FamilyMemberInfoItem item:
        // TODO
        // await Shell.Current.GoToAsync(UIRoutes.GetRoute<FamilyMemberPage>(), true, new() { { "FamilyName", item } });
        break;
    }
  }

  internal async void OnDeletePersonSelected(object sender, EventArgs e)
  {
  }

  internal async void OnEditPersonSelected(object sender, EventArgs e)
  {
  }

  internal async Task OnCreatePerson()
  {
    var dialog = new CreateNewPersonDialog(null, Services);

    await Navigation.PushModalAsync(dialog);
    var person = await dialog.Person;
    await Navigation.PopModalAsync();

    try
    {
      if (person is null)
      {
        return;
      }

      using var token = Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      await Services.GetRequiredService<ICurrentProjectProvider>()
        .Project
        .Persons
        .AddPersonAsync(person, token);
    }
    catch (Exception ex)
    {
      await DisplayAlert(UIStrings.AlertTitleError, ex.Message, UIStrings.BtnNameOk);
    }
    finally
    {
      OnPropertyChanged(nameof(Members));
    }
  }

  protected override void OnSizeAllocated(double width, double height)
  {
    const double PercentageOfWidth = 0.9;
    const int ItemsPerRow = 2;

    base.OnSizeAllocated(width, height);
    _PersonItemMinimalWidth = (int)(width * PercentageOfWidth / ItemsPerRow);

    OnPropertyChanged(nameof(PersonItemMinimalWidth));
  }
}