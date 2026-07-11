namespace GT4.Core.Project.Extensions;

using GT4.Core.Project.Dto;

/// <summary>
/// A photo's tagged-vs-plain category must be preserved whenever code reassigns its main-vs-additional
/// bucket (see <see cref="AsMainPhoto"/>/<see cref="AsAdditionalPhoto"/>), or Category and Content
/// desync and the wrong IDataConverter runs against it.
/// </summary>
public static class DataCategoryExtensions
{
  public static bool IsTaggedPhoto(this DataCategory category) =>
    category is DataCategory.PersonMainPhotoTagged or DataCategory.PersonPhotoTagged;

  public static DataCategory AsMainPhoto(this DataCategory category) =>
    category.IsTaggedPhoto() ? DataCategory.PersonMainPhotoTagged : DataCategory.PersonMainPhoto;

  public static DataCategory AsAdditionalPhoto(this DataCategory category) =>
    category.IsTaggedPhoto() ? DataCategory.PersonPhotoTagged : DataCategory.PersonPhoto;

  // A freshly picked/replaced photo can never carry the old photo's tags, so a content modification
  // must downgrade tagged -> plain rather than preserve it.
  public static DataCategory AsPlainPhoto(this DataCategory category) => category switch
  {
    DataCategory.PersonMainPhotoTagged or DataCategory.PersonMainPhoto => DataCategory.PersonMainPhoto,
    DataCategory.PersonPhotoTagged or DataCategory.PersonPhoto => DataCategory.PersonPhoto,
    _ => category
  };

  // The symmetric counterpart of AsPlainPhoto: used where a photo's Content just gained a
  // GedcomPhotoResidue envelope (GEDCOM import capturing residual OBJE tags).
  public static DataCategory AsTaggedPhoto(this DataCategory category) => category switch
  {
    DataCategory.PersonMainPhotoTagged or DataCategory.PersonMainPhoto => DataCategory.PersonMainPhotoTagged,
    DataCategory.PersonPhotoTagged or DataCategory.PersonPhoto => DataCategory.PersonPhotoTagged,
    _ => category
  };
}
