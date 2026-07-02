namespace GT4.UI.Logic;

// The person page arranges its image, relatives and biography in a grid whose layout adapts to the
// available space; GridLayout is one cell placement, PersonPageSmartLayout the placement of all three.
public record struct PersonPageSmartLayout(GridLayout Image, GridLayout Relatives, GridLayout Biography);

public record struct GridLayout(int Column, int ColumnSpan, int Row, int RowSpan);

public static class PersonPageLogic
{
  // Portrait (or a narrow landscape window) stacks the three blocks in one column; a wide-enough landscape
  // puts the image and relatives side by side with the biography spanning underneath. Width is measured in
  // device-independent units, so it is scaled by the display density before the 900px threshold check.
  public static PersonPageSmartLayout ComputeLayout(double width, double height, double density)
  {
    var widthInPixels = width * density;
    if (width < height || widthInPixels < 900)
    {
      return new PersonPageSmartLayout(
        Image: new GridLayout(Column: 0, ColumnSpan: 2, Row: 0, RowSpan: 1),
        Relatives: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1),
        Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 2, RowSpan: 1));
    }

    return new PersonPageSmartLayout(
      Image: new GridLayout(Column: 0, ColumnSpan: 1, Row: 0, RowSpan: 1),
      Relatives: new GridLayout(Column: 1, ColumnSpan: 1, Row: 0, RowSpan: 1),
      Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1));
  }
}
