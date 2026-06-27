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

  private static readonly HashSet<string> PassthroughTags =
    [GedcomTags.Submitter, GedcomTags.Submission, GedcomTags.Source, GedcomTags.Repository];

  /// <summary>A top-level record is preserved verbatim when it is one of the unmodeled kinds and carries an xref to key it by.</summary>
  public static bool IsPassthrough(GedcomNode record) => record.Xref is not null && PassthroughTags.Contains(record.Tag);

  public static string Key(GedcomNode record) => $"{KeyPrefix}{record.Tag}.{record.Xref}";
}
