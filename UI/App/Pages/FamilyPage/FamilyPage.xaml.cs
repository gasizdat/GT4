using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.App.Items;

namespace GT4.UI.App.Pages;

[QueryProperty(nameof(FamilyName), "FamilyName")]
public partial class FamilyPage : ContentPage
{
  private Name? _FamilyName = null;

  public FamilyPage()
  {
    InitializeComponent();
    BindingContext = this;
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
          .ToList();

        ret.Sort(Services.GetRequiredService<IComparer<FamilyMemberInfoItem>>());

        return ret;
      }
      catch (Exception ex)
      {
        return [/*new FamilyInfoItemRefresh(ex)*/];
      }
    }
  }

  internal async void OnMemberSelected(object sender, SelectionChangedEventArgs e)
  {

  }
}