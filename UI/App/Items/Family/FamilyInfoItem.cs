using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using System.Collections.ObjectModel;

namespace GT4.UI.Items;

public class FamilyInfoItem : CollectionItemBase<Name>
{
  private readonly FilteredObservableCollection<PersonInfo> _Persons = new();
  private readonly int _TotalPersonsCount;

  public FamilyInfoItem(Name familyName, PersonInfo[] persons)
    : base(familyName, "family_stub.png")
  {
    _TotalPersonsCount = persons.Length;
    _Persons.AddRange(persons);
  }

  public ObservableCollection<PersonInfo> Persons => _Persons.Items;

  // A family that never had any members is left visible (e.g. one just created); a family that had
  // members but none of them survive the current filter is what should be hidden.
  public bool HasVisiblePersons => _TotalPersonsCount == 0 || _Persons.Items.Count > 0;

  public void UpdatePersonFilter(ObservableCollectionFilterPredicate<PersonInfo> predicate) =>
    _Persons.Filter = predicate;
}
