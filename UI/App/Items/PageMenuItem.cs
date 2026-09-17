namespace GT4.UI.Items;

public class PageMenuItem : MenuItem
{
  // Marks an item as performing a mutation, so PageLayout can hide it under ReadOnlyMode.
  public bool EditingAction { get; set; }

  public string ButtonText
  {
    get
    {
      var index = Text?.IndexOf(' ') ?? -1;
      return index > 0 ? Text!.Substring(0, index) : "?";
    }
  }

  public string ToolTipText
  {
    get
    {
      var index = Text?.IndexOf(' ') ?? -1;
      return index > 0 ? Text!.Substring(index + 1) : Text ?? string.Empty;
    }
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
