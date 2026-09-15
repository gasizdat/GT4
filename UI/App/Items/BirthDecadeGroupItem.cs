namespace GT4.UI.Items;

/// <summary>
/// A group of adjacent decades sharing one x-axis label, wide enough to hold it: labelling every
/// decade individually crowds the axis once there are more decades than fit on screen. BarColumns
/// splits the group evenly, one column per bar.
/// </summary>
public record class BirthDecadeGroupItem(string Decade, ColumnDefinitionCollection BarColumns, BirthDecadeItem[] Bars);
