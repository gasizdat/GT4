using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using System.Windows.Input;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers RelativeRow directly: it's a plain INotifyPropertyChanged POCO with no MAUI dependency at
/// all, so it needs neither TestServices nor the main thread. Its own logic is the IsExpanded setter's
/// change-detection and the PropertyChanged pair it raises -- everything else is a stored constructor
/// argument.
/// </summary>
public class RelativeRowTests
{
  private static RelativeInfo MakeRelative(int id) =>
    new(
      new PersonInfo(id, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male, [], null),
      RelationshipType.Parent,
      null,
      Generation.Parent,
      Consanguinity.Zero);

  private static RelativeRow CreateRow(ICommand? toggleCommand = null) =>
    new(
      MakeRelative(1),
      Date.Create(1970, 1, 1, DateStatus.WellKnown),
      depth: 2,
      isLast: true,
      ancestorContinues: [true, false],
      toggleCommand ?? new Command(() => { }));

  [Fact]
  public void Ctor_stores_every_argument_as_is()
  {
    var relative = MakeRelative(1);
    var birthDate = Date.Create(1970, 1, 1, DateStatus.WellKnown);
    var ancestorContinues = new[] { true, false };
    var toggleCommand = new Command(() => { });

    var row = new RelativeRow(relative, birthDate, depth: 2, isLast: true, ancestorContinues, toggleCommand);

    Assert.Same(relative, row.Relative);
    Assert.Equal(birthDate, row.PersonBirthDate);
    Assert.Equal(2, row.Depth);
    Assert.True(row.IsLast);
    Assert.Same(ancestorContinues, row.AncestorContinues);
    Assert.Same(toggleCommand, row.ToggleCommand);
    Assert.False(row.IsExpanded);
  }

  [Fact]
  public void MoreBtnName_toggles_between_two_distinct_symbols()
  {
    var row = CreateRow();
    var collapsedName = row.MoreBtnName;

    row.IsExpanded = true;
    var expandedName = row.MoreBtnName;
    Assert.NotEqual(collapsedName, expandedName);

    row.IsExpanded = false;
    Assert.Equal(collapsedName, row.MoreBtnName);
  }

  [Fact]
  public void IsExpanded_change_raises_PropertyChanged_for_itself_and_MoreBtnName()
  {
    var row = CreateRow();
    var raised = new List<string?>();
    row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    row.IsExpanded = true;

    Assert.Equal(["IsExpanded", "MoreBtnName"], raised);
  }

  [Fact]
  public void Setting_IsExpanded_to_its_current_value_raises_nothing()
  {
    var row = CreateRow();
    row.IsExpanded = true;
    var raised = new List<string?>();
    row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

    row.IsExpanded = true;

    Assert.Empty(raised);
  }
}
