using GT4.Core.Project.Dto;

namespace GT4.UI.Utils;

public static partial class NameDeclension
{
  public static partial string ToFemaleDeclension(Language language, NameType nameType, string name);

  public static partial string ToMaleDeclension(Language language, NameType nameType, string name);
}