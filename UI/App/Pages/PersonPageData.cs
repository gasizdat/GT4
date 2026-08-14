using GT4.Core.Project.Dto;
using GT4.UI.Items;
using GT4.UI.Utils.Converters;

namespace GT4.UI.Pages;

internal sealed record PersonPageData(
  PersonFullInfo PersonFullInfo,
  RelativeInfo[] Roots,
  PhotoInfo[] Photos,
  AttachmentInfo[] Attachments,
  string? Bio,
  string? GedcomDetails,
  string? FamilyDetails,
  bool AddToNavigation);
