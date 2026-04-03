namespace GT4.UI.Utils;

partial class NameDeclension
{
  private static int GetLastNameRuleRU(string familyName)
  {
    if (familyName.EndsWith("овы"))
    {
      return 1;
    }
    if (familyName.EndsWith("евы"))
    {
      return 2;
    }
    if (familyName.EndsWith("ины"))
    {
      return 3;
    }
    if (familyName.EndsWith("ыны"))
    {
      return 4;
    }
    if (familyName.EndsWith("ские"))
    {
      return 5;
    }
    if (familyName.EndsWith("цкие"))
    {
      return 6;
    }
    if (familyName.EndsWith("ые"))
    {
      return 7;
    }
    if (familyName.EndsWith("ие"))
    {
      return 8;
    }
    if (familyName.EndsWith('ы') || familyName.EndsWith('и'))
    {
      return 9;
    }

    return 0;
  }

  private static int GetLastNameRuleEN(string familyName)
  {
    if (familyName.EndsWith("es"))
    {
      return 1;
    }
    if (familyName.StartsWith("s"))
    {
      return 2;
    }

    return 3;
  }
}
