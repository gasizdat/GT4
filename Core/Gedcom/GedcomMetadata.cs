using GT4.Core.Utils;

namespace GT4.Core.Gedcom;

/// <summary>
/// Top-level GEDCOM records GT4 has no model for — submitter, submission, source, repository — are
/// preserved rather than dropped: on import each record's serialized subtree is stored verbatim in the
/// project Metadata table under a <c>gedcom.&lt;TAG&gt;.&lt;xref&gt;</c> key, and on export those rows are
/// re-emitted unchanged. References to them from modeled records (an INDI's source citation, the header's
/// submitter pointer) are still dropped, so the re-emitted records are valid GEDCOM but unreferenced;
/// cross-references among the passthrough records themselves survive because each whole subtree is kept.
/// </summary>
internal static class GedcomMetadata
{
  public const string KeyPrefix = "gedcom.";

  /// <summary>
  /// Where a FAM record's unmodeled sub-tags live. Deliberately not under <see cref="KeyPrefix"/>: every row
  /// there is re-emitted verbatim as a top-level record, which these are not.
  /// </summary>
  public const string FamilyKeyPrefix = "gedcom-family.";

  private static readonly HashSet<string> PassthroughTags =
    [GedcomTags.Submitter, GedcomTags.Submission, GedcomTags.Source, GedcomTags.Repository];

  /// <summary>A top-level record is preserved verbatim when it is one of the unmodeled kinds and carries an xref to key it by.</summary>
  public static bool IsPassthrough(GedcomNode record) => record.Xref is not null && PassthroughTags.Contains(record.Tag);

  public static string Key(GedcomNode record) => $"{KeyPrefix}{record.Tag}.{record.Xref}";

  /// <summary>
  /// Keys a family's residue by the couple export rebuilds the record from — the source's own xref is
  /// renumbered on every export and cannot address it. The ids are ordered, so a same-sex couple keys the
  /// same row whichever of them import read as the husband; a single parent keys one id.
  /// </summary>
  public static string FamilyKey(int? husbandId, int? wifeId)
  {
    var ids = new[] { husbandId, wifeId }.OfType<int>().Order();
    return FamilyKeyPrefix + string.Join('.', ids);
  }

  /// <summary>
  /// Keys one MARR event's residue under its family by the marriage date, the only part of the event GT4's
  /// model holds and so the only thing telling a couple's several marriages apart. The tag literal keeps a
  /// year-only date from reading as a third person id.
  /// </summary>
  public static string MarriageKey(string familyKey, Date? date)
  {
    var value = date.HasValue ? GedcomDate.ToGedcom(date.Value) : null;
    return $"{familyKey}.{GedcomTags.Marriage}.{value}";
  }
}
