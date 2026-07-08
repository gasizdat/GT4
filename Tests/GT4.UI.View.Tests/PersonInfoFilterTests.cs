using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.View.Tests;

public class PersonInfoFilterTests
{
  private static readonly Date UnknownDate = Date.Create(null, null, null, DateStatus.Unknown);

  private static PersonInfoFilter CreateFilter()
  {
    var formatter = new Mock<IBiologicalSexFormatter>();
    formatter.Setup(f => f.ToString(It.IsAny<BiologicalSex?>())).Returns((BiologicalSex? s) => s?.ToString() ?? "?");
    return new PersonInfoFilter(formatter.Object);
  }

  private static PersonInfo Person(
    int id, string name = "Name", BiologicalSex sex = BiologicalSex.Unknown, Date? birthDate = null, Date? deathDate = null) =>
    new(id, birthDate ?? UnknownDate, deathDate, sex,
      name.Length == 0 ? [] : [new Name(id * 100, name, NameType.FirstName, null)], null);

  [Fact]
  public void Matches_with_no_criteria_is_true_for_any_person_including_one_with_no_names()
  {
    var filter = CreateFilter();
    var person = Person(1, name: "");

    Assert.True(filter.Matches(person));
  }

  [Fact]
  public void NameFilter_matches_by_wildcard()
  {
    var filter = CreateFilter();
    var person = Person(1, "John");

    filter.NameFilter = "J*n";
    Assert.True(filter.Matches(person));

    filter.NameFilter = "Mark";
    Assert.False(filter.Matches(person));
  }

  [Fact]
  public void SexFilterIndex_filters_by_biological_sex()
  {
    var filter = CreateFilter();
    var male = Person(1, "John", BiologicalSex.Male);
    var female = Person(2, "Jane", BiologicalSex.Female);

    filter.SexFilterIndex = 1; // Male
    Assert.True(filter.Matches(male));
    Assert.False(filter.Matches(female));
  }

  [Fact]
  public void MaritalStatusFilterIndex_reads_from_SetMarriedIds()
  {
    var filter = CreateFilter();
    var married = Person(1, "John");
    var single = Person(2, "Jane");
    filter.SetMarriedIds([1]);

    filter.MaritalStatusFilterIndex = 1; // Married
    Assert.True(filter.Matches(married));
    Assert.False(filter.Matches(single));

    filter.MaritalStatusFilterIndex = 2; // Single
    Assert.False(filter.Matches(married));
    Assert.True(filter.Matches(single));
  }

  [Fact]
  public void SetMarriedIds_replaces_the_previous_set()
  {
    var filter = CreateFilter();
    filter.SetMarriedIds([1]);
    filter.SetMarriedIds([2]);
    filter.MaritalStatusFilterIndex = 1; // Married

    Assert.False(filter.Matches(Person(1, "John")));
    Assert.True(filter.Matches(Person(2, "Jane")));
  }

  [Fact]
  public void YearFilter_matches_persons_alive_in_the_selected_year()
  {
    var filter = CreateFilter();
    var person = Person(1, "John",
      birthDate: Date.Create(1950, 1, 1, DateStatus.WellKnown),
      deathDate: Date.Create(2000, 1, 1, DateStatus.WellKnown));

    filter.IsYearFilterEnabled = true;
    filter.SelectedYear = 1975;
    Assert.True(filter.Matches(person));

    filter.SelectedYear = 2010;
    Assert.False(filter.Matches(person));
  }

  [Fact]
  public void SetYearBounds_clamps_SelectedYear_when_it_falls_outside_the_new_range()
  {
    var filter = CreateFilter();
    filter.SelectedYear = 5000;

    filter.SetYearBounds(1900, 2000);

    Assert.Equal(2000, filter.SelectedYear);
    Assert.Equal(1900, filter.MinYear);
    Assert.Equal(2000, filter.MaxYear);
  }

  [Fact]
  public void IsAnyFilterActive_reflects_each_criterion_independently()
  {
    var filter = CreateFilter();
    Assert.False(filter.IsAnyFilterActive);

    filter.NameFilter = "John";
    Assert.True(filter.IsAnyFilterActive);
    filter.NameFilter = "";
    Assert.False(filter.IsAnyFilterActive);

    filter.SexFilterIndex = 1;
    Assert.True(filter.IsAnyFilterActive);
    filter.SexFilterIndex = 0;

    filter.MaritalStatusFilterIndex = 1;
    Assert.True(filter.IsAnyFilterActive);
    filter.MaritalStatusFilterIndex = 0;

    filter.IsYearFilterEnabled = true;
    Assert.True(filter.IsAnyFilterActive);
  }

  [Fact]
  public void Clear_resets_every_criterion()
  {
    var filter = CreateFilter();
    filter.NameFilter = "John";
    filter.SexFilterIndex = 1;
    filter.MaritalStatusFilterIndex = 1;
    filter.IsYearFilterEnabled = true;
    filter.SetYearBounds(1900, 2000);
    filter.SelectedYear = 1950;

    filter.Clear();

    Assert.Equal(string.Empty, filter.NameFilter);
    Assert.Equal(0, filter.SexFilterIndex);
    Assert.Equal(0, filter.MaritalStatusFilterIndex);
    Assert.False(filter.IsYearFilterEnabled);
    Assert.Equal(2000, filter.SelectedYear);
    Assert.False(filter.IsAnyFilterActive);
  }

  [Fact]
  public void Changed_fires_for_every_criterion_setter_and_for_SetMarriedIds()
  {
    var filter = CreateFilter();
    var raises = 0;
    filter.Changed += (_, _) => raises++;

    filter.NameFilter = "a";
    filter.SexFilterIndex = 1;
    filter.MaritalStatusFilterIndex = 1;
    filter.IsYearFilterEnabled = true;
    filter.SelectedYear = 2000;
    filter.SetMarriedIds([1]);
    filter.Clear();

    Assert.Equal(7, raises);
  }

  [Fact]
  public void ComputeYearBounds_falls_back_to_a_century_back_when_nothing_is_known()
  {
    var (min, max) = PersonInfoFilter.ComputeYearBounds([Person(1, "John")]);

    var currentYear = Date.Now.Year;
    Assert.Equal(currentYear - 100, min);
    Assert.Equal(currentYear, max);
  }

  [Fact]
  public void ComputeYearBounds_uses_known_birth_and_death_years()
  {
    var persons = new[]
    {
      Person(1, "John", birthDate: Date.Create(1900, 1, 1, DateStatus.WellKnown)),
      Person(2, "Jane", deathDate: Date.Create(1980, 1, 1, DateStatus.WellKnown)),
    };

    var (min, max) = PersonInfoFilter.ComputeYearBounds(persons);

    Assert.Equal(1900, min);
    Assert.Equal(Math.Max(1980, Date.Now.Year), max);
  }
}
