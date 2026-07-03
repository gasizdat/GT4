using GT4.Core.Project.Dto;
using System.Collections.ObjectModel;

namespace GT4.UI.Logic;

// Per-page back/forward history of viewed persons. Owns the observable list the CollectionView binds to
// plus the current index; all mutation goes through Append/Move/MoveToPerson.
public class PersonNavigation
{
  private int _Index = -1;

  public ObservableCollection<PersonInfo> History { get; } = new();

  public PersonInfo? Current => _Index >= 0 ? History[_Index] : null;

  // Records a freshly-loaded person as the new head of history: drops any forward entries, appends a plain
  // PersonInfo copy (normalising a PersonFullInfo down so the CollectionView's value-equality selection
  // matches an item), and advances the index. The person is already loaded, so this requests no load.
  public void Append(PersonInfo person)
  {
    while (History.Count > _Index + 1)
      History.RemoveAt(History.Count - 1);
    History.Add(new PersonInfo(person, person.Names, person.MainPhoto));
    _Index = History.Count - 1;
  }

  // Steps the index by delta (-1/+1 = previous/next) when a different person exists there, returning the
  // person to load; returns null at the ends and when delta leaves the index where it is.
  public PersonInfo? Move(int delta)
  {
    var target = _Index + delta;
    if (target < 0 || target >= History.Count || target == _Index)
      return null;
    _Index = target;
    return History[target];
  }

  // Moves to an existing entry (e.g. a CollectionView tap). Returns the person to load only when the
  // selection actually moved, so a redundant reselect / echo is a no-op. Mirrors the current setter's
  // `index != current` guard exactly (including the never-hit not-found case).
  public PersonInfo? MoveToPerson(PersonInfo? person)
  {
    if (person is null)
      return null;
    var index = History.IndexOf(person);
    if (index == _Index)
      return null;
    _Index = index;
    return person;
  }
}
