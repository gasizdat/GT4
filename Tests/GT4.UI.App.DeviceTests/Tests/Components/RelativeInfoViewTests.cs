using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers RelativeInfoView's own logic: which date _RelationshipDate picks depending on
/// Relative.Type (the view's own PersonBirthDate for a Parent, the relative's own BirthDate for a
/// Child, otherwise the relationship's own Date), ShowDate's separate type allow-list (a relative can
/// have a resolvable date and still not show it -- Parent/Child/Sibling never show a date even though
/// two of them do resolve one), RelationTypeName's null-Relative short-circuit, and the RefreshView()
/// cascade that re-notifies every one of the view's own public properties whenever Relative changes.
/// IDateFormatter/IRelationshipTypeFormatter are the app's real registered implementations (unmocked,
/// like SettingsPageTests) -- the actual formatting output has its own dedicated tests elsewhere; here
/// it's only used as an oracle to confirm which underlying date/args the view routed to.
/// </summary>
public class RelativeInfoViewTests
{
  private static async Task<(TestableRelativeInfoView View, TestServices Services)> CreateViewAsync()
  {
    var services = new TestServices();
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var view = await MainThread.InvokeOnMainThreadAsync(() => new TestableRelativeInfoView(services.Provider));
    return (view, services);
  }

  private static RelativeInfo MakeRelative(
    RelationshipType type,
    Date birthDate,
    Date? relationshipDate = null,
    BiologicalSex sex = BiologicalSex.Male,
    Generation? generation = null,
    Consanguinity? consanguinity = null) =>
    new(
      new PersonInfo(1, birthDate, null, sex, [], null),
      type,
      relationshipDate,
      generation ?? Generation.Parent,
      consanguinity ?? Consanguinity.Zero);

  private static readonly Date WellKnownDate = Date.Create(1970, 1, 1, DateStatus.WellKnown);
  private static readonly Date UnknownDate = Date.Create(1970, 1, 1, DateStatus.Unknown);

  [Theory]
  [InlineData(RelationshipType.Parent)]
  [InlineData(RelationshipType.Child)]
  [InlineData(RelationshipType.Sibling)]
  [InlineData(RelationshipType.SiblingByMother)]
  [InlineData(RelationshipType.SpouseParent)]
  public async Task ShowDate_is_false_for_relationship_types_outside_the_allow_list_even_with_a_resolvable_date(RelationshipType type)
  {
    var (view, _) = await CreateViewAsync();
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.PersonBirthDate = WellKnownDate;
      view.Relative = MakeRelative(type, WellKnownDate, WellKnownDate);
    });

    Assert.False(view.ShowDate);
  }

  [Theory]
  [InlineData(RelationshipType.Spouse)]
  [InlineData(RelationshipType.AdoptiveChild)]
  [InlineData(RelationshipType.StepChild)]
  [InlineData(RelationshipType.AdoptiveParent)]
  [InlineData(RelationshipType.StepParent)]
  [InlineData(RelationshipType.AdoptiveSibling)]
  [InlineData(RelationshipType.StepSibling)]
  public async Task ShowDate_is_true_for_allow_listed_types_with_a_well_known_date(RelationshipType type)
  {
    var (view, _) = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() => view.Relative = MakeRelative(type, WellKnownDate, WellKnownDate));

    Assert.True(view.ShowDate);
  }

  [Fact]
  public async Task ShowDate_is_false_when_the_relationship_date_is_missing()
  {
    var (view, _) = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() => view.Relative = MakeRelative(RelationshipType.Spouse, WellKnownDate, relationshipDate: null));

    Assert.False(view.ShowDate);
  }

  [Fact]
  public async Task ShowDate_is_false_when_the_relationship_date_status_is_Unknown()
  {
    var (view, _) = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() => view.Relative = MakeRelative(RelationshipType.Spouse, WellKnownDate, UnknownDate));

    Assert.False(view.ShowDate);
  }

  [Fact]
  public async Task RelationshipDate_uses_the_views_own_PersonBirthDate_for_a_Parent()
  {
    var (view, services) = await CreateViewAsync();
    var personBirthDate = Date.Create(1950, 5, 5, DateStatus.WellKnown);
    var relativesOwnBirthDate = Date.Create(1999, 9, 9, DateStatus.WellKnown);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.PersonBirthDate = personBirthDate;
      view.Relative = MakeRelative(RelationshipType.Parent, relativesOwnBirthDate);
    });

    var dateFormatter = services.Provider.GetRequiredService<IDateFormatter>();
    Assert.Equal(dateFormatter.ToString(personBirthDate), view.RelationshipDate);
  }

  [Fact]
  public async Task RelationshipDate_uses_the_relatives_own_BirthDate_for_a_Child()
  {
    var (view, services) = await CreateViewAsync();
    var relativesOwnBirthDate = Date.Create(1999, 9, 9, DateStatus.WellKnown);

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.PersonBirthDate = Date.Create(1950, 5, 5, DateStatus.WellKnown);
      view.Relative = MakeRelative(RelationshipType.Child, relativesOwnBirthDate);
    });

    var dateFormatter = services.Provider.GetRequiredService<IDateFormatter>();
    Assert.Equal(dateFormatter.ToString(relativesOwnBirthDate), view.RelationshipDate);
  }

  [Fact]
  public async Task RelationshipDate_uses_the_relationships_own_Date_for_every_other_type()
  {
    var (view, services) = await CreateViewAsync();
    var relationshipDate = Date.Create(2010, 6, 6, DateStatus.WellKnown);

    await MainThread.InvokeOnMainThreadAsync(() =>
      view.Relative = MakeRelative(RelationshipType.Spouse, WellKnownDate, relationshipDate));

    var dateFormatter = services.Provider.GetRequiredService<IDateFormatter>();
    Assert.Equal(dateFormatter.ToString(relationshipDate), view.RelationshipDate);
  }

  [Fact]
  public async Task RelationshipDate_falls_back_to_null_when_there_is_no_Relative()
  {
    var (view, services) = await CreateViewAsync();

    var dateFormatter = services.Provider.GetRequiredService<IDateFormatter>();
    Assert.Equal(dateFormatter.ToString(null), view.RelationshipDate);
  }

  [Fact]
  public async Task RelationTypeName_is_empty_when_there_is_no_Relative()
  {
    var (view, _) = await CreateViewAsync();

    Assert.Equal(string.Empty, view.RelationTypeName);
  }

  [Fact]
  public async Task RelationTypeName_delegates_to_the_relationship_type_formatter()
  {
    var (view, services) = await CreateViewAsync();
    var relative = MakeRelative(
      RelationshipType.Spouse,
      WellKnownDate,
      WellKnownDate,
      sex: BiologicalSex.Female,
      generation: Generation.Zero,
      consanguinity: Consanguinity.Zero);

    await MainThread.InvokeOnMainThreadAsync(() => view.Relative = relative);

    var relationshipTypeFormatter = services.Provider.GetRequiredService<IRelationshipTypeFormatter>();
    var expected = relationshipTypeFormatter.ToString(relative.Type, relative.BiologicalSex, relative.Generation, relative.Consanguinity);
    Assert.Equal(expected, view.RelationTypeName);
  }

  [Fact]
  public async Task Setting_Relative_raises_PropertyChanged_for_every_one_of_the_views_own_properties()
  {
    var (view, _) = await CreateViewAsync();
    var raised = new HashSet<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.Relative = MakeRelative(RelationshipType.Spouse, WellKnownDate, WellKnownDate));

    // RefreshView() re-notifies every property declared directly on RelativeInfoView (see ViewUtils.RefreshView).
    var expected = new HashSet<string?>
    {
      nameof(view.PersonBirthDate),
      nameof(view.Relative),
      nameof(view.PersonInfoFrame),
      nameof(view.NameFormat),
      nameof(view.SelectCommand),
      nameof(view.ShowDate),
      nameof(view.RelationshipDate),
      nameof(view.RelationTypeName),
      nameof(view.HasBloodShare),
      nameof(view.BloodShareText),
    };
    Assert.Equal(expected, raised);
  }

  [Fact]
  public async Task HasBloodShare_is_false_and_BloodShareText_is_empty_when_there_is_no_Relative()
  {
    var (view, _) = await CreateViewAsync();

    Assert.False(view.HasBloodShare);
    Assert.Equal(string.Empty, view.BloodShareText);
  }

  [Fact]
  public async Task HasBloodShare_is_false_for_a_relation_that_carries_no_blood()
  {
    var (view, _) = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
      view.Relative = MakeRelative(RelationshipType.Spouse, WellKnownDate, generation: Generation.Zero, consanguinity: Consanguinity.Zero));

    Assert.False(view.HasBloodShare);
    Assert.Equal(string.Empty, view.BloodShareText);
  }

  [Fact]
  public async Task BloodShareText_formats_a_fractional_percentage_using_the_current_culture()
  {
    var (view, _) = await CreateViewAsync();

    // First cousin: Consanguinity.UncleAunt reached via a Child hop -> 12.5%.
    await MainThread.InvokeOnMainThreadAsync(() =>
      view.Relative = MakeRelative(RelationshipType.Child, WellKnownDate, generation: Generation.Zero, consanguinity: Consanguinity.UncleAunt));

    var expected = string.Format(GT4.UI.Resources.UIStrings.RelBloodShare_1, 12.5);
    Assert.Equal(expected, view.BloodShareText);
  }

  [Fact]
  public async Task BloodShareText_reports_the_coefficient_for_a_blood_relation()
  {
    var (view, _) = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() =>
      view.Relative = MakeRelative(RelationshipType.Parent, WellKnownDate, generation: Generation.Parent, consanguinity: Consanguinity.Zero));

    Assert.True(view.HasBloodShare);
    Assert.Equal("🩸 50%", view.BloodShareText);
  }

  [Fact]
  public async Task Setting_Relative_to_the_same_instance_raises_nothing()
  {
    var (view, _) = await CreateViewAsync();
    var relative = MakeRelative(RelationshipType.Spouse, WellKnownDate, WellKnownDate);
    await MainThread.InvokeOnMainThreadAsync(() => view.Relative = relative);
    var raised = new List<string?>();
    view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    await MainThread.InvokeOnMainThreadAsync(() => view.Relative = relative);

    Assert.Empty(raised);
  }
}
