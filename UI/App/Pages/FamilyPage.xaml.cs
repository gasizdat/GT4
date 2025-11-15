using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Items;
using GT4.UI.Resources;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(FamilyName), "FamilyName")]
public partial class FamilyPage : ContentPage
{
  private readonly IServiceProvider _Services;
  private Name? _FamilyName = null;
  private int _PersonItemMinimalWidth;

  protected FamilyPage(IServiceProvider serviceProvider)
  {
    _Services = serviceProvider;
    MemberItemTappedCommand = new Command<FamilyMemberInfoItem>(OnMemberSelected);
    DeleteFamilyCommand = new Command<object?>(OnDeleteFmily);
    EditFamilyCommand = new Command<object?>(OnEditFmily);

    InitializeComponent();
  }

  public FamilyPage()
    : this(ServiceBuilder.DefaultServices)
  {
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

  public int PersonItemMinimalWidth => _PersonItemMinimalWidth;

  public ICommand MemberItemTappedCommand { get; init; }
  public ICommand DeleteFamilyCommand { get; init; }
  public ICommand EditFamilyCommand { get; init; }

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
        using var token = _Services
          .GetRequiredService<ICancellationTokenProvider>()
          .CreateDbCancellationToken();
        var ret = _Services
          .GetRequiredService<ICurrentProjectProvider>()
          .Project
          .PersonManager
          .GetPersonInfosByNameAsync(FamilyName, token)
          .Result
          .Select(person => new FamilyMemberInfoItem(person, _Services))
          .OrderBy(item => item, _Services.GetRequiredService<IComparer<FamilyMemberInfoItem>>())
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

  private async void OnMemberSelected(FamilyMemberInfoItem member)
  {
    switch (member)
    {
      case FamilyMemberInfoItemRefresh:
        OnPropertyChanged(nameof(Members));
        break;
      case FamilyMemberInfoItemCreate:
        await OnCreatePerson();
        break;
      case FamilyMemberInfoItem familyMember:
        await OnEditPerson(familyMember);
        break;
    }
  }

  private async void OnDeleteFmily(object? parameter)
  {
    var canDelete = _FamilyName is not null &&
       await this.ShowConfirmation(string.Format(UIStrings.AlertTextDeleteConfirmationText_1, _FamilyName.Value));

    if (!canDelete)
    {
      return;
    }

    try
    {
      using var token = _Services
        .GetRequiredService<ICancellationTokenProvider>()
        .CreateDbCancellationToken();
      await _Services
        .GetRequiredService<ICurrentProjectProvider>()
        .Project
        .FamilyManager
        .RemoveFamilyAsync(_FamilyName!, token);
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
    finally
    {
      await Shell.Current.GoToAsync("..", true);
    }
  }

  private async void OnEditFmily(object? parameter)
  {
  }

  private async Task OnCreatePerson()
  {
    var dialog = new CreateOrUpdatePersonDialog(null, _Services);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
      if (info is null || _FamilyName is null)
      {
        return;
      }

      var projectProvider = _Services.GetRequiredService<ICurrentProjectProvider>();
      using var token = _Services.GetRequiredService<ICancellationTokenProvider>().CreateDbCancellationToken();
      var person = projectProvider
        .Project
        .FamilyManager
        .SetUpPersonFamily(info, _FamilyName);

      await projectProvider
        .Project
        .PersonManager
        .AddPersonAsync(person, token);
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
    finally
    {
      OnPropertyChanged(nameof(Members));
    }
  }

  private async Task OnEditPerson(FamilyMemberInfoItem familyMember)
  {
    var projectProvider = _Services.GetRequiredService<ICurrentProjectProvider>();
    var tokenProvider = _Services.GetRequiredService<ICancellationTokenProvider>();
    using var readToken = tokenProvider.CreateDbCancellationToken();
    var familyMemberFullInfo = await projectProvider
      .Project
      .PersonManager
      .GetPersonFullInfoAsync(familyMember.Info, readToken);

    var dialog = new CreateOrUpdatePersonDialog(familyMemberFullInfo, _Services);
    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
      if (info is null || _FamilyName is null)
      {
        return;
      }

      using var updateToken = _Services
        .GetRequiredService<ICancellationTokenProvider>()
        .CreateDbCancellationToken();
      var person = projectProvider
        .Project
        .FamilyManager
        .SetUpPersonFamily(info, _FamilyName);

      await projectProvider
        .Project
        .PersonManager
        .UpdatePersonAsync(info, updateToken);
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
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
