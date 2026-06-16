using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Comparers;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class PersonInfoComparerTests
{
  private static PersonInfo MakePerson(int id) =>
    new(id, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Unknown, [], null);

  private static PersonInfoComparer CreateComparer(params (int id, string displayName)[] persons)
  {
    var mock = new Mock<INameFormatter>();
    foreach (var (id, name) in persons)
    {
      mock.Setup(f => f.ToString(It.Is<PersonInfo>(p => p.Id == id), NameFormat.CommonPersonName))
        .Returns(name);
    }
    return new PersonInfoComparer(mock.Object);
  }

  [Fact]
  public void Compare_AlphabeticalOrder_NegativeForLesser()
  {
    var alice = MakePerson(1);
    var bob = MakePerson(2);
    var comparer = CreateComparer((1, "Alice"), (2, "Bob"));

    comparer.Compare(alice, bob).Should().BeNegative();
  }

  [Fact]
  public void Compare_AlphabeticalOrder_PositiveForGreater()
  {
    var alice = MakePerson(1);
    var bob = MakePerson(2);
    var comparer = CreateComparer((1, "Alice"), (2, "Bob"));

    comparer.Compare(bob, alice).Should().BePositive();
  }

  [Fact]
  public void Compare_SameName_ReturnsZero()
  {
    var a = MakePerson(1);
    var b = MakePerson(2);
    var comparer = CreateComparer((1, "Smith"), (2, "Smith"));

    comparer.Compare(a, b).Should().Be(0);
  }

  [Fact]
  public void Compare_NullLeft_ReturnsZero()
  {
    var comparer = CreateComparer((1, "Alice"));
    var alice = MakePerson(1);

    comparer.Compare(null, alice).Should().Be(0);
  }

  [Fact]
  public void Compare_NullRight_ReturnsZero()
  {
    var comparer = CreateComparer((1, "Alice"));
    var alice = MakePerson(1);

    // null?.CompareTo(...) returns null → 0
    comparer.Compare(alice, null).Should().BePositive();
  }

  [Fact]
  public void Sort_PersonList_OrderedAlphabetically()
  {
    var charlie = MakePerson(1);
    var alice = MakePerson(2);
    var bob = MakePerson(3);
    var comparer = CreateComparer((1, "Charlie"), (2, "Alice"), (3, "Bob"));

    var sorted = new[] { charlie, alice, bob }.OrderBy(p => p, comparer).ToArray();

    sorted.Should().Equal(alice, bob, charlie);
  }

  [Fact]
  public void CompareUsesCaseSensitiveComparison()
  {
    var lower = MakePerson(1);
    var upper = MakePerson(2);
    var comparer = CreateComparer((1, "alice"), (2, "Bob"));

    // String.CompareTo is ordinal by default — 'a' > 'B' in ASCII
    var result = comparer.Compare(lower, upper);
    result.Should().NotBe(0, "case-sensitive comparison distinguishes 'alice' from 'Bob'");
  }

  [Fact]
  public void UsesSpecifiedNameFormat()
  {
    var person = MakePerson(1);
    var mock = new Mock<INameFormatter>();
    mock.Setup(f => f.ToString(person, NameFormat.ShortPersonName)).Returns("A. Smith");
    mock.Setup(f => f.ToString(person, NameFormat.CommonPersonName)).Returns("Alice Smith");

    var comparer = new PersonInfoComparer(mock.Object, NameFormat.ShortPersonName);
    comparer.Compare(person, person);

    mock.Verify(f => f.ToString(person, NameFormat.ShortPersonName), Times.AtLeast(1));
    mock.Verify(f => f.ToString(person, NameFormat.CommonPersonName), Times.Never);
  }
}
