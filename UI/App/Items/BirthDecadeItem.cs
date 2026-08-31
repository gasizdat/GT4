namespace GT4.UI.Items;

/// <summary>
/// One bar of the births-by-decade histogram. <paramref name="BarColumns"/> carries the bar's share
/// of the row as star widths, so every bar starts on the same vertical line and scales with the
/// column rather than a fixed pixel length.
/// </summary>
public record class BirthDecadeItem(string Decade, string Count, ColumnDefinitionCollection BarColumns);
