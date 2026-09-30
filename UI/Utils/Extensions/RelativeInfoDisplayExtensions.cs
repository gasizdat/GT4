using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Resources;

namespace GT4.UI.Utils.Extensions;

public static class RelativeInfoDisplayExtensions
{
  public static Date? GetRelationshipDate(this RelativeInfo relative, Date? personBirthDate) => relative.Type switch
  {
    RelationshipType.Parent => personBirthDate,
    RelationshipType.Child => relative.BirthDate,
    _ => relative.Date
  };

  public static bool ShowsRelationshipDate(this RelativeInfo relative, Date? personBirthDate) =>
    relative.GetRelationshipDate(personBirthDate) is { Status: not DateStatus.Unknown } &&
    relative.Type switch
    {
      RelationshipType.Spouse => true,
      RelationshipType.AdoptiveChild => true,
      RelationshipType.StepChild => true,
      RelationshipType.AdoptiveParent => true,
      RelationshipType.StepParent => true,
      RelationshipType.AdoptiveSibling => true,
      RelationshipType.StepSibling => true,
      _ => false
    };

  public static string GetBloodShareText(this RelativeInfo relative) => relative.GetBloodShare() is { } share
    ? string.Format(UIStrings.RelBloodShare_1, Math.Round(share * 100, 2))
    : string.Empty;
}
