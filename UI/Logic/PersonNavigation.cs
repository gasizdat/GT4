using GT4.Core.Project.Dto;
using System.Collections.ObjectModel;

namespace GT4.UI.Logic;

// Per-page back/forward history of viewed persons.
public class PersonNavigation
{
  private int _Index = -1;

  public ObservableCollection<PersonInfo> History { get; } = new();

  public PersonInfo? Current => _Index >= 0 ? History[_Index] : null;

  // Stores a plain PersonInfo copy so value-equality selection matches even when a PersonFullInfo is passed in.
  public void Append(PersonInfo person)
  {
    while (History.Count > _Index + 1)
      History.RemoveAt(History.Count - 1);
    History.Add(new PersonInfo(person, person.Names, person.MainPhoto));
    _Index = History.Count - 1;
  }

  public PersonInfo? Move(int delta)
  {
    var target = _Index + delta;
    if (target < 0 || target >= History.Count || target == _Index)
      return null;
    _Index = target;
    return History[target];
  }

  // Returns the person to load only when the selection actually moved, so a redundant reselect is a no-op.
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
