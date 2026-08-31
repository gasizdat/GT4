namespace GT4.UI.Items;

/// <summary>
/// One bar of the births-by-decade histogram. BarColumns is the bar's share of its row as star
/// widths, so a bar scales with the column rather than a fixed pixel length.
/// </summary>
public record class BirthDecadeItem(string Decade, string Count, ColumnDefinitionCollection BarColumns);
