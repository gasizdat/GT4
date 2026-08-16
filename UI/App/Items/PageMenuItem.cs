namespace GT4.UI.Items;

public class PageMenuItem : MenuItem
{
  // A caption without a leading glyph (e.g. a formatted "Open {0}") has no button text to split
  // out; Icon lets the page supply one explicitly instead of feeding the whole caption to the
  // button.
  public string? Icon { get; set; }

  public string ButtonText
  {
    get
    {
      if (Icon is not null)
      {
        return Icon;
      }

      var index = Text.IndexOf(' ');
      return index > 0 ? Text.Substring(0, index) : "?";
    }
  }

  public string ToolTipText
  {
    get
    {
      if (Icon is not null)
      {
        return Text;
      }

      var index = Text.IndexOf(' ');
      return index > 0 ? Text.Substring(index + 1) : Text;
    }
  }
}
