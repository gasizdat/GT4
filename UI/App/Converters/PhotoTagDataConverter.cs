using GT4.Core.Gedcom;
using GT4.Core.Project.Dto;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.Http;

namespace GT4.UI.Converters;

/// <summary>
/// Decodes a tagged photo (<see cref="DataCategory.PersonMainPhotoTagged"/>/
/// <see cref="DataCategory.PersonPhotoTagged"/>) into just its image portion, same output type as
/// <see cref="ImageDataConverter"/> so every existing photo display call site needs no further change.
/// Inherits <see cref="ImageDataConverter.FromObjectAsync"/> unchanged -- a modification can never
/// legitimately carry the old photo's tags (there is no editable-caption UI in this pass), so it is
/// encoded as plain image bytes exactly like ImageDataConverter would -- the resulting Data's Category
/// gets downgraded from tagged to plain by PersonDataItem.ToDataAsync (DataCategoryExtensions.AsPlainPhoto).
/// Lives in UI.App (not UI.Utils) because unwrapping the residue envelope needs Core.Gedcom.
/// </summary>
public sealed class PhotoTagDataConverter : ImageDataConverter, IDataConverter
{
  public PhotoTagDataConverter(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
  {
  }

  public override Task<object?> ToObjectAsync(Data? data, CancellationToken token)
  {
    if (data is null)
      return Task.FromResult<object?>(null);

    var imageBytes = GedcomPhotoResidue.ExtractImageBytes(data.Content);
    return Task.FromResult<object?>(ImageUtils.ImageFromBytes(imageBytes));
  }
}
