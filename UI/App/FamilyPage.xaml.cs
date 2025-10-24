namespace GT4.UI;

using GT4.Core.Project;
using GT4.Core.Project.Dto;

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
    }
  }

  public ServiceProvider Services { get; set; } = ServiceBuilder.DefaultServices;
 
  public ICollection<PersonInfoItem> Members
  {
    get
    {
      if (FamilyName is null)
      {
        return [];
      }

      try
      {
        using var token = new Core.Utils.DefaultCancellationToken();
        return Services.GetRequiredService<ICurrentProjectProvider>()
          .Project
          .Persons
          .GetPersonsByNameAsync(FamilyName, token)
          .Result
          .Select(person => new FamilyMemberInfoItem(person, Services))
          .ToArray();
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