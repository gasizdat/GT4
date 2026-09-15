namespace GT4.UI.Items;

/// <summary>
/// One bar of the births-by-decade histogram. Column is its position within its group's equal-width
/// columns; BarRows is the bar's share of its column as star heights, so a bar scales with the
/// fixed-height chart area rather than a pixel length of its own.
/// </summary>
public record class BirthDecadeItem(int Column, string Count, RowDefinitionCollection BarRows);
