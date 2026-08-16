namespace GT4.UI.Items;

public class PageMenuItem : MenuItem
{
  public string ButtonText
  {
    get
    {
      var index = Text.IndexOf(' ');
      return index > 0 ? Text.Substring(0, index) : "?";
    }
  }

  public string ToolTipText
  {
    get
    {
      var index = Text.IndexOf(' ');
      return index > 0 ? Text.Substring(index + 1) : Text;
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
