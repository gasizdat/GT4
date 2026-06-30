namespace GT4.Core.Gedcom;

/// <summary>The subset of GEDCOM 5.5.1 tags this library reads and writes.</summary>
internal static class GedcomTags
{
  public const string Header = "HEAD";
  public const string Trailer = "TRLR";
  public const string Source = "SOUR";
  public const string Gedcom = "GEDC";
  public const string Version = "VERS";
  public const string Form = "FORM";
  public const string Charset = "CHAR";

  public const string Individual = "INDI";
  public const string Family = "FAM";
  public const string Note = "NOTE";
  public const string Submitter = "SUBM";
  public const string Submission = "SUBN";
  public const string Repository = "REPO";

  public const string Name = "NAME";
  public const string Given = "GIVN";
  public const string Surname = "SURN";
  public const string Sex = "SEX";
  public const string Birth = "BIRT";
  public const string Death = "DEAT";
  public const string Date = "DATE";

  public const string Husband = "HUSB";
  public const string Wife = "WIFE";
  public const string Child = "CHIL";
  public const string Marriage = "MARR";
  public const string FamilySpouse = "FAMS";
  public const string FamilyChild = "FAMC";
  public const string Pedigree = "PEDI";

  // The PEDI value marking a child's link to a family as adoptive (the default, "birth", is left implicit).
  public const string AdoptedPedigree = "adopted";

  public const string Object = "OBJE";
  public const string Blob = "BLOB";
  public const string File = "FILE";
  public const string Primary = "_PRIM";

  // The _PRIM value marking the OBJE that GT4 treats as the person's main (profile) photo.
  public const string PrimaryYes = "Y";

  public const string Concatenation = "CONC";
  public const string Continuation = "CONT";
}
