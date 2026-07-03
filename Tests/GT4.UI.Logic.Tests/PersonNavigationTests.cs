using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Logic;
using Xunit;

namespace GT4.UI.Logic.Tests;

public sealed class PersonNavigationTests
{
  private static PersonInfo Info(int id) =>
    new(id, Date.Now, null, BiologicalSex.Male, [], null);

  [Fact]
  public void Append_adds_person_and_makes_it_current()
  {
    var nav = new PersonNavigation();

    nav.Append(Info(1));

    nav.History.Select(p => p.Id).Should().Equal(1);
    nav.Current!.Id.Should().Be(1);
  }

  [Fact]
  public void Append_stores_a_plain_PersonInfo_copy()
  {
    var full = new PersonFullInfo(Info(1), [], [], null, null);
    var nav = new PersonNavigation();

    nav.Append(full);

    nav.Current!.GetType().Should().Be(typeof(PersonInfo));
    nav.Current.Id.Should().Be(1);
    nav.Current.Should().NotBeSameAs(full);
  }

  [Fact]
  public void Append_truncates_forward_history()
  {
    var nav = new PersonNavigation();
    nav.Append(Info(1));
    nav.Append(Info(2));
    nav.Append(Info(3));

    nav.Move(-1); // back to id 2
    nav.Append(Info(4));

    nav.History.Select(p => p.Id).Should().Equal(1, 2, 4);
    nav.Current!.Id.Should().Be(4);
  }

  [Fact]
  public void Move_returns_null_at_bounds()
  {
    var nav = new PersonNavigation();
    nav.Append(Info(1));

    nav.Move(-1).Should().BeNull();
    nav.Move(1).Should().BeNull();
    nav.Current!.Id.Should().Be(1);
  }

  [Fact]
  public void Move_returns_null_when_delta_does_not_move()
  {
    var nav = new PersonNavigation();
    nav.Append(Info(1));
    nav.Append(Info(2));

    nav.Move(0).Should().BeNull();
    nav.Current!.Id.Should().Be(2);
  }

  [Fact]
  public void Move_steps_backward_and_forward()
  {
    var nav = new PersonNavigation();
    nav.Append(Info(1));
    nav.Append(Info(2));

    nav.Move(-1)!.Id.Should().Be(1);
    nav.Current!.Id.Should().Be(1);
    nav.Move(1)!.Id.Should().Be(2);
    nav.Current!.Id.Should().Be(2);
  }

  [Fact]
  public void MoveToPerson_moves_to_existing_entry()
  {
    var nav = new PersonNavigation();
    nav.Append(Info(1));
    nav.Append(Info(2));
    var first = nav.History[0];

    var selected = nav.MoveToPerson(first);

    selected.Should().BeSameAs(first);
    nav.Current!.Id.Should().Be(1);
  }

  [Fact]
  public void MoveToPerson_returns_null_when_already_current()
  {
    var nav = new PersonNavigation();
    nav.Append(Info(1));

    nav.MoveToPerson(nav.Current).Should().BeNull();
  }

  [Fact]
  public void MoveToPerson_returns_null_for_null()
  {
    new PersonNavigation().MoveToPerson(null).Should().BeNull();
  }
}
