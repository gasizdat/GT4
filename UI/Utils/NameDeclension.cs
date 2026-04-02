namespace GT4.UI.Utils;

public static partial class NameDeclension
{
  private static string ToSingleRU(string name)
  {
    string single;
    if (name.EndsWith("ы"))
    {
      single = name[..^1];
    }
    else if (name.EndsWith("ие"))
    {
      single = name[..^2] + "ий";
    }
    else
    {
      single = name;
    }

    return single;
  }

  private static string ToSingleEN(string name)
  {
    if (name.EndsWith('s'))
    {
      return name[..^1];
    }

    return name;
  }

}