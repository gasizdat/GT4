using GT4.Core.Project;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Dialogs;
using GT4.UI.Formatters;
using GT4.UI.Items;
using GT4.UI.Resources;
using Microsoft.VisualBasic;
using System.Windows.Input;

namespace GT4.UI.Pages;

[QueryProperty(nameof(FamilyName), "FamilyName")]
public partial class FamilyPage : ContentPage
{
  private readonly IServiceProvider _Services;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly INameFormatter _NameFormatrer;
  private Name? _FamilyName = null;
  private int _PersonItemMinimalWidth;

  protected FamilyPage(IServiceProvider serviceProvider)
  {
    _Services = serviceProvider;
    _CancellationTokenProvider = _Services.GetRequiredService<ICancellationTokenProvider>();
    _CurrentProjectProvider = _Services.GetRequiredService<ICurrentProjectProvider>();
    _NameFormatrer = _Services.GetRequiredService<INameFormatter>();

    MemberItemTappedCommand = new Command<PersonInfoItem>(OnOpenPerson);
    DeleteFamilyCommand = new Command<object?>(OnDeleteFmily);
    EditFamilyCommand = new Command<object?>(OnEditFamily);
    CreatePersonCommand = new Command<object?>(OnCreatePerson);

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
      OnPropertyChanged(nameof(Persons));
      OnPropertyChanged(nameof(FamilyName));
      OnPropertyChanged(nameof(RemoveFamilyToolbarItemName));
      OnPropertyChanged(nameof(EditFamilyToolbarItemName));
    }
  }

  public int PersonItemMinimalWidth => _PersonItemMinimalWidth;

  public ICommand MemberItemTappedCommand { get; init; }

  public ICommand DeleteFamilyCommand { get; init; }

  public ICommand EditFamilyCommand { get; init; }

  public ICommand CreatePersonCommand { get; init; }

  public ICollection<PersonInfoItem> Persons
  {
    get
    {
      if (FamilyName is null)
      {
        return [];
      }

      try
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var ret = _CurrentProjectProvider
          .Project
          .PersonManager
          .GetPersonInfosByNameAsync(name: FamilyName, selectMainPhoto: true, token)
          .Result
          .Select(person => new PersonInfoItem(person, _NameFormatrer))
          .OrderBy(item => item, _Services.GetRequiredService<IComparer<PersonInfoItem>>())
          .ToList();

        return ret;
      }
      catch (Exception ex)
      {
        _ = PageAlert.ShowError(ex);
        return Enumerable.Empty<PersonInfoItem>().ToList();
      }
    }
  }

  public string RemoveFamilyToolbarItemName =>
    string.Format(UIStrings.MenuItemNameRemove_1, _FamilyName?.Value ?? string.Empty);

  public string EditFamilyToolbarItemName =>
    string.Format(UIStrings.MenuItemNameEdit_1, _FamilyName?.Value ?? string.Empty);

  private async void OnDeleteFmily(object? _)
  {
    var canDelete = _FamilyName is not null &&
       await this.ShowConfirmation(string.Format(UIStrings.AlertTextDeleteConfirmationText_1, _FamilyName.Value));

    if (!canDelete)
    {
      return;
    }

    try
    {
      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _CurrentProjectProvider
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

  private async void OnEditFamily(object? _)
  {
    Name[]? names;
    using (var token = _CancellationTokenProvider.CreateDbCancellationToken())
    {
      names = await _CurrentProjectProvider
        .Project
        .Names
        .TryGetNameWithSubnamesByIdAsync(FamilyName?.Id, token);
    }

    var familyName = names?.SingleOrDefault(n => n.Type.HasFlag(NameType.FamilyName));
    var maleLastName = names?.SingleOrDefault(n => n.Type.HasFlag(NameType.LastName | NameType.MaleDeclension));
    var femaleLastName = names?.SingleOrDefault(n => n.Type.HasFlag(NameType.LastName | NameType.FemaleDeclension));
    if (familyName is null)
    {
      return;
    }

    var dialog = new CreateOrUpdateNameDialog(familyName, maleLastName, femaleLastName, _Services);

    await Navigation.PushModalAsync(dialog);
    var info = await dialog.Info;
    await Navigation.PopModalAsync();

    try
    {
      if (info is null)
      {
        return;
      }

      familyName = familyName with { Value = info.Name };
      maleLastName = maleLastName is null ? null : maleLastName with { Value = info.MaleName };
      femaleLastName = femaleLastName is null ? null : femaleLastName with { Value = info.FemaleName };

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      await _CurrentProjectProvider
        .Project
        .FamilyManager
        .UpdateFamilyAsync(familyName, maleLastName, femaleLastName, token);

      FamilyName = familyName;
    }
    catch (Exception ex)
    {
      await this.ShowError(ex);
    }
  }

  private async void OnCreatePerson(object? _)
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

      using var token = _CancellationTokenProvider.CreateDbCancellationToken();
      var person = _CurrentProjectProvider
        .Project
        .FamilyManager
        .SetUpPersonFamily(info, _FamilyName);

      await _CurrentProjectProvider
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
      OnPropertyChanged(nameof(Persons));
    }
  }

  private async void OnOpenPerson(PersonInfoItem familyMember)
  {
    await Shell.Current.GoToAsync(UIRoutes.GetRoute<PersonPage>(), true, new() { { "PersonInfo", familyMember.Info } });
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
