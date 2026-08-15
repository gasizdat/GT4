# Subsystem: Core.Gedcom

`Core/Gedcom` (`GT4.Core.Gedcom.csproj`) translates between the GEDCOM 5.5.1 text format and a
`Core.Project` document. It never touches SQLite directly — `GedcomImporter` writes through
`IProjectDocument`'s `Persons`/`Names`/`PersonData`/`Relatives`/`Metadata`, inside the transaction
model [core-project.md](core-project.md) describes; `GedcomExporter` reads back through the same
surface. The organizing goal of this subsystem, more than any individual mapping rule, is
**round-trip fidelity**: what GT4 cannot model is still not allowed to disappear.

## Invariant: whatever isn't modeled is captured as residue, not dropped

GT4's schema is a deliberately small subset of what GEDCOM can express. Every GEDCOM tag the
import doesn't turn into a typed field is instead captured verbatim as **residue** — a serialized
copy of the unconsumed nodes — and carried alongside the record it came from, to be spliced back
in on export:

- A person's residue rides on `PersonFullInfo.GedcomData` (`DataCategory.PersonGedcomTags`),
  built by `GedcomImporter.BuildResidueRoots`.
- A family's residue — everything a `FAM` record states beyond the spouse/child edges GT4
  reconstructs — lives in the `Metadata` table, not on a person, because a family isn't its own
  entity in GT4's model. See the family-key problem below for why it needs a special key.
- A single `MARR` event's own residue (its `PLAC`, `SOUR`, an unparseable date) is stored per
  event, keyed by the couple *and* the marriage date, because a couple can marry more than once
  and the date is the only thing GT4's model keeps to tell the events apart
  (`GedcomImporter.StoreMarriageResidueAsync`).

This is why the round-trip functional harness (see
[principles/testing-strategy.md](../principles/testing-strategy.md)) tests *stability* and
*fidelity* separately rather than just "does it parse": residue is what makes near-total fidelity
possible despite a small schema, and a regression in how residue is collected or reattached is
invisible to any test that only checks the typed fields import correctly.

## Invariant: a family's identity for residue lookup is the couple's ids, not the source xref

GEDCOM `FAM`/`INDI` xrefs are renumbered on every export — nothing in GT4 preserves them — so a
family's residue can't be keyed by the xref it arrived under; that key would never be found again
after the first re-export. `GedcomMetadata.FamilyKey(husbandId, wifeId)` (`Core/Gedcom/GedcomMetadata.cs`)
keys it by the two persons' own database ids instead, sorted so a same-sex couple keys the same row
regardless of which spouse import happened to read as "husband." A family with only one resolved
parent keys on that one id alone. **Any change to how spouses are matched during import has to
keep this key computable the same way on both the import and export side, or family residue
silently stops round-tripping** — check `git log -S` on `FamilyKey` before touching family
matching, since a change here can look like a harmless simplification while quietly breaking
residue lookup.

## Invariant: source data is normalized before any domain pass runs

Two structural rewrites happen once, up front, over the whole imported subtree, before the
photo/attachment/residue passes ever see it (`GedcomImporter.ImportAsync`, the loop calling
`SplitMultiFileObjects` and `InlineNotePointers`):

- **`SplitMultiFileObjects`** rewrites a multi-`FILE` `OBJE` into one single-`FILE` `OBJE` per
  file. GT4 models one file per media object; without this normalization, every downstream pass
  that reads media would have to handle the multi-file case itself.
- **`InlineNotePointers`** replaces a `NOTE` pointer with the text of the record it points at, since
  GT4 preserves no standalone `NOTE` record for a pointer to keep resolving against.

**The reason this is a named pattern and not just "some preprocessing":** every pass downstream —
photo selection, attachment selection, residue collection — gets to stay unaware that either
irregularity can occur at all. When a new upstream irregularity turns up, the fix belongs in this
normalization sweep, not as a special case threaded through every consumer. This paid off across
more than one bug already tracked under issue #148 — `git log --all --grep '#148'` finds the
commits.

## Invariant: only a direct INDI-child image OBJE is a photo — everything else is an attachment

`SelectPhotos` only considers `OBJE`s that are **direct children of the individual** and image-typed;
an `OBJE` nested under an event (`BIRT`/`SOUR`, a marriage citation) or belonging to a `FAM` record
is documentary, not a portrait, and `SelectAttachments`/`SelectReferencedMedia` claim it instead —
regardless of format. A family's own media (a couple photo, a marriage certificate under
`MARR → SOUR → OBJE`) has nowhere to live as a family entity, so it's linked as an attachment to
both resolved spouses, sharing one committed `Data` row rather than duplicating the bytes
(`GedcomImporter.ImportFamilyMediaAsync`). This classification is a correctness rule, not a
convenience: an image-typed attachment that got reclassified as a photo would change what the UI
does with it (see [ui-app.md](ui-app.md) for the photo/attachment distinction downstream).

## Invariant: re-importing into a populated project merges, it doesn't duplicate

`GedcomImporter.ResolveMatchesAsync` matches an incoming individual against an existing person by
first name, family name, and birth date — an absent/unknown birth date matches only another
absent/unknown one, never a dated person — and only when the match is unambiguous on both sides.
A matched individual is **gap-filled**, not overwritten: `GapFillAsync` adds a biography, photos,
attachments, or residue only for fields the existing person doesn't already have, and relative
edges are added only if an equivalent edge (same owner, relative, type, and date-code) doesn't
already exist. This is what makes importing the same file twice, or importing an updated export
from another tool, safe rather than a duplication hazard.

## Non-goals

- Not a GEDCOM-tag-by-tag reference — `GedcomTags.cs` and `GedcomMapping.cs` are the live mapping;
  this doc explains why the mapping is structured the way it is, not what every tag does.
- Not a description of the round-trip test harness itself — see
  [principles/testing-strategy.md](../principles/testing-strategy.md) and
  `Tests/Functional/run-gedcom-roundtrip.ps1`.
- Not the media byte-handling path once a photo/attachment reaches the UI layer — see
  [ui-app.md](ui-app.md) and [principles/rendering-inversion.md](../principles/rendering-inversion.md).
