namespace GT4.Core.Gedcom;

/// <summary>
/// A presentation-ready projection of one preserved INDI sub-tag: its raw GEDCOM <see cref="Tag"/>
/// (e.g. <c>OCCU</c>), optional <see cref="Value"/> and nested <see cref="Children"/>. The structure
/// mirrors the source nesting (<c>OCCU</c> -&gt; <c>DATE</c>, <c>RESI</c> -&gt; <c>PLAC</c>) so a consumer
/// can render it faithfully; mapping the tag to a localized label is left to the UI.
/// </summary>
public sealed record GedcomFact(string Tag, string? Value, GedcomFact[] Children);
