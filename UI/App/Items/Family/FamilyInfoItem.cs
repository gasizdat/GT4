using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Collections.ObjectModel;

namespace GT4.UI.Items;

public class FamilyInfoItem : CollectionItemBase<Name>
{
  private readonly FilteredObservableCollection<PersonInfo> _Persons = new();
  private readonly int _TotalPersonsCount;

  public FamilyInfoItem(Name familyName, PersonInfo[] persons, ObservableCollectionFilterPredicate<PersonInfo>? personsFilter)
    : base(familyName, "family_stub.png")
  {
    _TotalPersonsCount = persons.Length;
    _Persons.Filter = personsFilter;
    _Persons.AddRange(persons);
  }

  // Sentinel family for persons that have no FamilyName-typed name; Id 0 never collides with a
  // real name because SQLite rowids start at 1.
  public static Name NoFamilyName { get; } = new(0, UIStrings.FamilyNameNoFamily, NameType.FamilyName, null);

  public static bool HasNoFamily(PersonInfo person) =>
    !person.Names.Any(name => name.Type.HasFlag(NameType.FamilyName));

  public ObservableCollection<PersonInfo> Persons => _Persons.Items;

  // The full, unfiltered membership -- e.g. for a lazy filter-data fetch that needs everyone in the
  // family regardless of the currently-visible (filtered) set.
  public IReadOnlyList<PersonInfo> AllPersons => _Persons.AllItems;

  // A family that never had any members is left visible (e.g. one just created); a family that had
  // members but none of them survive the current filter is what should be hidden.
  public bool HasVisiblePersons => _TotalPersonsCount == 0 || _Persons.Items.Count > 0;

  public void Update() => _Persons.Update();
}
