using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.UI.Utils.Comparers;
using Xunit;

namespace GT4.UI.View.Tests;

public class NameComparerTests
{
  private readonly NameComparer _comparer = new();

  private static Name MakeName(string value) =>
    new(0, value, NameType.FirstName, null);

  [Fact]
  public void Compare_AlphabeticalOrder_NegativeForLesser()
  {
    _comparer.Compare(MakeName("Alpha"), MakeName("Beta")).Should().BeNegative();
  }

  [Fact]
  public void Compare_AlphabeticalOrder_PositiveForGreater()
  {
    _comparer.Compare(MakeName("Beta"), MakeName("Alpha")).Should().BePositive();
  }

  [Fact]
  public void Compare_SameValue_ReturnsZero()
  {
    _comparer.Compare(MakeName("Smith"), MakeName("Smith")).Should().Be(0);
  }

  [Fact]
  public void Compare_NullLeft_ReturnsZero()
  {
    _comparer.Compare(null, MakeName("Alpha")).Should().Be(0);
  }

  [Fact]
  public void Compare_NullRight_NonZero()
  {
    // "Alpha"?.CompareTo(null) = positive
    _comparer.Compare(MakeName("Alpha"), null).Should().BePositive();
  }

  [Fact]
  public void Sort_NameList_OrderedAlphabetically()
  {
    var names = new[]
    {
      MakeName("Charlie"),
      MakeName("Alice"),
      MakeName("Bob"),
    };

    var sorted = names.OrderBy(n => n, _comparer).Select(n => n.Value).ToArray();

    sorted.Should().Equal("Alice", "Bob", "Charlie");
  }

  [Fact]
  public void Compare_IgnoresNameType()
  {
    var firstName = new Name(0, "Smith", NameType.FirstName, null);
    var lastName = new Name(0, "Smith", NameType.LastName, null);

    _comparer.Compare(firstName, lastName).Should().Be(0, "same value should be equal regardless of NameType");
  }

  [Fact]
  public void Compare_CyrillicValues_SortedCorrectly()
  {
    var names = new[] { "Яков", "Иван", "Алексей" }.Select(MakeName).ToList();

    var sorted = names.OrderBy(n => n, _comparer).Select(n => n.Value).ToArray();

    sorted.Should().Equal("Алексей", "Иван", "Яков");
  }
}
