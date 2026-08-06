using GT4.Core.Project.Dto;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GT4.UI.Items;

public class FamilyInfoItem : CollectionItemBase<Name>, INotifyPropertyChanged
{
  // BindableLayout/SafeBindableLayout render every member unconditionally -- no virtualization. A
  // family with hundreds of members (chiefly the synthetic "no family" bucket, which can hold every
  // unassociated person in the project) crashes the app outright; ~30 native chip trees is already
  // several hundred KB. Cap the card display and surface the remainder as a count instead.
  public const int MaxDisplayedPersons = 30;

  private readonly FilteredObservableCollection<PersonInfo> _Persons = new();
  private readonly int _TotalPersonsCount;
  private ObservableCollection<PersonInfo> _DisplayedPersons = new();

  public FamilyInfoItem(Name familyName, PersonInfo[] persons, ObservableCollectionFilterPredicate<PersonInfo>? personsFilter)
    : base(familyName, "family_stub.png")
  {
    _TotalPersonsCount = persons.Length;
    _Persons.Filter = personsFilter;
    _Persons.AddRange(persons);
    _DisplayedPersons = ComputeDisplayedPersons();
  }

  // Sentinel family for persons that have no FamilyName-typed name; Id 0 never collides with a
  // real name because SQLite rowids start at 1.
  public static Name NoFamilyName { get; } = new(0, UIStrings.FamilyNameNoFamily, NameType.FamilyName, null);

  public static bool HasNoFamily(PersonInfo person) =>
    !person.Names.Any(name => name.Type.HasFlag(NameType.FamilyName));

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<PersonInfo> Persons => _Persons.Items;

  // What the card actually renders -- Persons capped at MaxDisplayedPersons. The same instance as
  // Persons whenever nothing is hidden, so an unaffected family keeps the granular
  // insert/remove diffing SafeBindableLayout does on Persons directly.
  public ObservableCollection<PersonInfo> DisplayedPersons => _DisplayedPersons;

  // The full, unfiltered membership -- e.g. for a lazy filter-data fetch that needs everyone in the
  // family regardless of the currently-visible (filtered) set.
  public IReadOnlyList<PersonInfo> AllPersons => _Persons.AllItems;

  public bool HasMorePersons => _Persons.Items.Count > MaxDisplayedPersons;

  public int HiddenPersonsCount => Math.Max(0, _Persons.Items.Count - MaxDisplayedPersons);

  public string MorePersonsText => string.Format(UIStrings.FamilyMorePersons_1, HiddenPersonsCount);

  // A family that never had any members is left visible (e.g. one just created); a family that had
  // members but none of them survive the current filter is what should be hidden.
  public bool HasVisiblePersons => _TotalPersonsCount == 0 || _Persons.Items.Count > 0;

  public void Update()
  {
    _Persons.Update();
    RefreshDisplayedPersons();
  }

  private ObservableCollection<PersonInfo> ComputeDisplayedPersons()
  {
    if (_Persons.Items.Count <= MaxDisplayedPersons)
    {
      return _Persons.Items;
    }

    // FilterView has no debounce, so UpdateFamilies() re-runs Update() on every family on every
    // keystroke. Reusing the existing snapshot when the visible top MaxDisplayedPersons is
    // unchanged avoids rebinding SafeBindableLayout.ItemsSource -- which would otherwise force a
    // full teardown/rebuild of every displayed card on every keystroke, even for a family the
    // filter change didn't affect.
    var capped = _Persons.Items.Take(MaxDisplayedPersons);
    return _DisplayedPersons.Count == MaxDisplayedPersons && _DisplayedPersons.SequenceEqual(capped)
      ? _DisplayedPersons
      : new ObservableCollection<PersonInfo>(capped);
  }

  private void RefreshDisplayedPersons()
  {
    var updated = ComputeDisplayedPersons();
    if (ReferenceEquals(updated, _DisplayedPersons))
    {
      return;
    }

    _DisplayedPersons = updated;
    OnPropertyChanged(nameof(DisplayedPersons));
    OnPropertyChanged(nameof(HasMorePersons));
    OnPropertyChanged(nameof(HiddenPersonsCount));
    OnPropertyChanged(nameof(MorePersonsText));
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
