using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Comparers;
using Xunit;

namespace GT4.UI.View.Tests;

public class ProjectInfoComparerTests
{
  private readonly ProjectInfoComparer _comparer = new();

  private static FileDescription AnyFile() =>
    new(new DirectoryDescription(Environment.SpecialFolder.Personal, []), "file.db", null);

  private static ProjectInfo MakeProject(string name, string description = "") =>
    new(name, description, string.Empty, AnyFile());

  [Fact]
  public void Compare_AlphabeticalOrder_NegativeForLesser()
  {
    _comparer.Compare(MakeProject("Alpha"), MakeProject("Beta")).Should().BeNegative();
  }

  [Fact]
  public void Compare_AlphabeticalOrder_PositiveForGreater()
  {
    _comparer.Compare(MakeProject("Beta"), MakeProject("Alpha")).Should().BePositive();
  }

  [Fact]
  public void Compare_SameName_ReturnsZero()
  {
    _comparer.Compare(MakeProject("Smith"), MakeProject("Smith")).Should().Be(0);
  }

  [Fact]
  public void Compare_NullLeft_ReturnsZero()
  {
    _comparer.Compare(null, MakeProject("Alpha")).Should().Be(0);
  }

  [Fact]
  public void Sort_ProjectList_OrderedByName()
  {
    var projects = new[]
    {
      MakeProject("Charlie"),
      MakeProject("Alice"),
      MakeProject("Bob"),
    };

    var sorted = projects.OrderBy(p => p, _comparer).Select(p => p.Name).ToArray();

    sorted.Should().Equal("Alice", "Bob", "Charlie");
  }

  [Fact]
  public void Compare_IgnoresDescription()
  {
    var a = MakeProject("Smith", "first description");
    var b = MakeProject("Smith", "second description");

    _comparer.Compare(a, b).Should().Be(0, "comparison is name-only");
  }
}
