namespace GT4.UI.Items;

public class PageMenuItem : MenuItem
{
  // Marks an item as performing a mutation, so PageLayout can hide it under ReadOnlyMode.
  public bool EditingAction { get; set; }

  public string ButtonText => GetGlyph(Text);

  public string ToolTipText => GetTooltip(Text);

  public static string GetGlyph(string? text)
  {
    var index = text?.IndexOf(' ') ?? -1;
    return index > 0 ? text!.Substring(0, index) : "?";
  }

  public static string GetTooltip(string? text)
  {
    var index = text?.IndexOf(' ') ?? -1;
    return index > 0 ? text!.Substring(index + 1) : text ?? string.Empty;
  }

  protected override void OnPropertyChanged(string? propertyName = null)
  {
    base.OnPropertyChanged(propertyName);

    if (propertyName == nameof(Text))
    {
      OnPropertyChanged(nameof(ButtonText));
      OnPropertyChanged(nameof(ToolTipText));
    }
  }
}
