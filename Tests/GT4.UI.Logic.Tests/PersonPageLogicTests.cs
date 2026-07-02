using FluentAssertions;
using GT4.UI.Logic;
using Xunit;

namespace GT4.UI.Logic.Tests;

public sealed class PersonPageLogicTests
{
  private static readonly GridLayout StackedImage = new(Column: 0, ColumnSpan: 2, Row: 0, RowSpan: 1);
  private static readonly GridLayout StackedRelatives = new(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1);
  private static readonly GridLayout StackedBiography = new(Column: 0, ColumnSpan: 2, Row: 2, RowSpan: 1);

  [Fact]
  public void ComputeLayout_stacks_blocks_when_portrait()
  {
    var layout = PersonPageLogic.ComputeLayout(width: 500, height: 900, density: 2);

    layout.Image.Should().Be(StackedImage);
    layout.Relatives.Should().Be(StackedRelatives);
    layout.Biography.Should().Be(StackedBiography);
  }

  [Fact]
  public void ComputeLayout_stacks_blocks_when_landscape_but_too_narrow()
  {
    // Landscape (width > height) yet only 800 physical px wide, below the 900 threshold → still stacked.
    var layout = PersonPageLogic.ComputeLayout(width: 800, height: 500, density: 1);

    layout.Image.Should().Be(StackedImage);
    layout.Relatives.Should().Be(StackedRelatives);
    layout.Biography.Should().Be(StackedBiography);
  }

  [Fact]
  public void ComputeLayout_places_image_and_relatives_side_by_side_when_wide_landscape()
  {
    var layout = PersonPageLogic.ComputeLayout(width: 900, height: 500, density: 1);

    layout.Image.Should().Be(new GridLayout(Column: 0, ColumnSpan: 1, Row: 0, RowSpan: 1));
    layout.Relatives.Should().Be(new GridLayout(Column: 1, ColumnSpan: 1, Row: 0, RowSpan: 1));
    layout.Biography.Should().Be(new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1));
  }
}
