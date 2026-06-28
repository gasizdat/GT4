namespace GT4.Core.Project.Dto;

// Controls how much of a person's main photo a query loads into PersonInfo.MainPhoto.
public enum MainPhoto
{
  // Do not load the main photo (MainPhoto stays null).
  Ignore,
  // Load the full original blob (for detail, editing and the photo viewer).
  Load,
  // Load identity only: MainPhoto carries the Data id and MimeType with empty Content, so the caller can
  // resolve a downsized thumbnail from the thumbnail cache without reading the original blob.
  Reference
}
