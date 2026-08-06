using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Items;
using Xunit;

namespace GT4.UI.DeviceTests;

public class FamilyInfoItemTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfo P(int id) =>
    new(id, UnknownDate, null, BiologicalSex.Male, [N(id * 100, $"Person{id}", NameType.FirstName)], null);

  private static PersonInfo[] Persons(int count) => Enumerable.Range(1, count).Select(P).ToArray();

  [Fact]
  public void Family_at_or_under_the_cap_shows_everyone_and_no_more_indicator()
  {
    var family = new FamilyInfoItem(N(1, "Ivanov", NameType.FamilyName), Persons(FamilyInfoItem.MaxDisplayedPersons), (_, _) => true);

    Assert.Same(family.Persons, family.DisplayedPersons);
    Assert.Equal(FamilyInfoItem.MaxDisplayedPersons, family.DisplayedPersons.Count);
    Assert.False(family.HasMorePersons);
    Assert.Equal(0, family.HiddenPersonsCount);
  }

  [Fact]
  public void Family_over_the_cap_shows_only_the_cap_and_reports_the_rest_as_hidden()
  {
    const int total = FamilyInfoItem.MaxDisplayedPersons + 12;
    var family = new FamilyInfoItem(N(1, "Ivanov", NameType.FamilyName), Persons(total), (_, _) => true);

    Assert.Equal(FamilyInfoItem.MaxDisplayedPersons, family.DisplayedPersons.Count);
    Assert.True(family.HasMorePersons);
    Assert.Equal(12, family.HiddenPersonsCount);
    // Not asserting the exact rendered text here: it goes through UIStrings.FamilyMorePersons_1
    // under the OS's ambient UI culture, which this repo's satellite resources aren't reliably
    // correct under (a pre-existing, unrelated resource-encoding issue -- confirmed even
    // FamilyNameNoFamily comes back garbled under ru-RU). Just confirm the count made it through.
    Assert.Contains("12", family.MorePersonsText);
  }

  [Fact]
  public void Update_uncapping_switches_DisplayedPersons_back_to_the_live_Persons_instance()
  {
    var showAll = false;
    const int total = FamilyInfoItem.MaxDisplayedPersons + 5;
    var family = new FamilyInfoItem(N(1, "Ivanov", NameType.FamilyName), Persons(total), (_, p) => showAll || p.Id <= 10);

    Assert.False(family.HasMorePersons);
    Assert.Same(family.Persons, family.DisplayedPersons);

    showAll = true;
    family.Update();

    Assert.True(family.HasMorePersons);
    Assert.NotSame(family.Persons, family.DisplayedPersons);
    Assert.Equal(FamilyInfoItem.MaxDisplayedPersons, family.DisplayedPersons.Count);
  }

  [Fact]
  public void Update_raises_PropertyChanged_only_when_crossing_the_cap_boundary()
  {
    var showAll = true;
    const int total = FamilyInfoItem.MaxDisplayedPersons + 5;
    var family = new FamilyInfoItem(N(1, "Ivanov", NameType.FamilyName), Persons(total), (_, _) => showAll);

    var raised = new List<string?>();
    family.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    // No change: still capped, same membership and order -- ComputeDisplayedPersons reuses the
    // existing snapshot, so no refresh is expected here.
    family.Update();
    Assert.DoesNotContain(nameof(FamilyInfoItem.DisplayedPersons), raised);

    raised.Clear();
    showAll = false;
    family.Update();
    Assert.Contains(nameof(FamilyInfoItem.DisplayedPersons), raised);
    Assert.Contains(nameof(FamilyInfoItem.HasMorePersons), raised);
  }
}
