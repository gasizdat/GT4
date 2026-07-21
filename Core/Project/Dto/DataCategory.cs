namespace GT4.Core.Project.Dto;

public enum DataCategory
{
  // The main person's photo used as a profile icon
  PersonMainPhoto = 1,
  // Additional person photo
  PersonPhoto = 2,
  // Person biography
  PersonBio = 3,
  // Verbatim GEDCOM INDI sub-tags GT4 does not model, kept so an import/export round-trip loses nothing
  PersonGedcomTags = 4,
  // Main photo whose Content is a GedcomPhotoResidue envelope (residual OBJE tags + image bytes)
  PersonMainPhotoTagged = 5,
  // Additional photo whose Content is a GedcomPhotoResidue envelope (residual OBJE tags + image bytes)
  PersonPhotoTagged = 6,
  // Non-photo file attachment (PDF, document scan, ...); Content is always a GedcomPhotoResidue envelope
  // carrying the original filename (the OBJE's FILE tag, kept in the residual) alongside the raw bytes
  PersonAttachment = 7
}
