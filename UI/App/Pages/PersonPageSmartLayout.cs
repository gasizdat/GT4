namespace GT4.UI.Pages;

public record struct PersonPageSmartLayout(GridLayout Image, GridLayout Relatives);

public record struct GridLayout(int Column, int ColumnSpan, int Row, int RowSpan);