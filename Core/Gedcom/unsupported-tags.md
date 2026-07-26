GEDCOM tags GT4 does not map into its model

The reader parses every line, so none of these fail an import — they are simply
not turned into GT4 data. Most still round-trip untouched: the "Preserved
verbatim" records below, every unmodeled direct INDI sub-tag, and even the
unmodeled *sub-details* of tags GT4 partially models (e.g. a PLAC or SOUR under
BIRT or MARR) are stored as opaque residue and re-emitted on export — GT4 just
never reads their values into its own data model. What is genuinely dropped is
whole top-level OBJE records (and pointers to them), the original HEAD envelope
(regenerated fresh by the exporter), and the couple itself when a FAM asserts one
by HUSB/WIFE alone — which takes the rest of that FAM record with it, since the
export has no regenerated record to put it back on. See the per-section notes.

Preserved verbatim (passthrough)

These top-level records have no GT4 schema, but instead of being dropped they are
stored verbatim in the project Metadata table under a gedcom.<TAG>.<xref> key and
re-emitted unchanged on export. A FAM-level or HEAD-level reference *to* them is
still dropped (see below), so in that case the re-emitted record is valid but
unreferenced; an INDI/event-level reference to them round-trips fine (see below).

- SUBM (record) — Submitter: person/org who contributed the file.
- SUBN — Submission: metadata for a submission to a lineage system (LDS).
- SOUR (record) — Source: a citable document/record.
- REPO — Repository: an archive or library that holds sources.

Top-level NOTE records

A `0 @N1@ NOTE` record is not preserved as a record, but its text is: every
pointer to it is replaced by that text at import, wherever the pointer sits — a
direct INDI `NOTE` becomes the person's biography, one nested inside a preserved
tag (a `NAME`, a `TITL`) carries the text in the residue instead of the pointer.
So the export references no record it does not contain. A record nothing points
at is dropped with its text; so is any sub-structure of the record itself.

Photos (OBJE) — partially modeled

An inline INDI `OBJE` carrying an embedded base64 `BLOB` is imported into GT4's
photo model (the `_PRIM Y` one, or the first, becomes the main photo; the rest
are additional) and re-exported the same way, so photos round-trip self-contained
inside the single .ged file. NOT modeled: top-level `0 @M1@ OBJE` records and
pointer references to them, and `OBJE`s that only reference an external `FILE`
(GT4 cannot resolve the bytes) — those FILE-ref OBJEs fall through to the INDI
residue passthrough below, so they survive verbatim but are not loaded as photos.
The embedded `BLOB` form is non-conformant to strict GEDCOM 5.5.1 (which expects
external FILE references); it is chosen so the export stays a single shareable
file and round-trips through GT4 itself.

Dropped — whole records

- HEAD's own `SUBM` pointer — the reference that points at the preserved records
  above is not kept there, so a record nothing else cites re-exports
  unreferenced. A citation anywhere in an INDI or a FAM is different: it is
  preserved as residue like any other unmodeled sub-tag (see "Sub-detail tags"
  below) and round-trips fine, pointer included.

Header children

- GEDC — GEDCOM metadata block (the file's own version/form).
- VERS — Version number (of GEDCOM or the producing product).
- FORM — Form/format, e.g. LINEAGE-LINKED.
- CHAR — Character encoding of the file.
- SOUR (header) — Originating software/system that produced the file.

INDI sub-tags

Preserved verbatim: every direct INDI child below is stored as a per-person
GEDCOM blob (DataCategory.PersonGedcomTags) at import and re-attached under the INDI
on export, including its own sub-structure (OCCU/DATE, RESI/PLAC, ...). GT4 reads
none of them into its model; they exist only to survive the round-trip.

- CHR — Christening (infant baptism).
- CHRA — Adult christening.
- BAPM — Baptism.
- BARM — Bar Mitzvah.
- BASM — Bas/Bat Mitzvah.
- BLES — Blessing.
- CONF — Confirmation (religious rite).
- FCOM — First Communion.
- ORDN — Ordination to clergy.
- BURI — Burial.
- CREM — Cremation.
- ADOP — Adoption event (date/place; the link is what we keep via FAMC PEDI).
- EMIG — Emigration.
- IMMI — Immigration.
- NATU — Naturalization.
- CENS — Appearance in a census.
- PROB — Probate of a will.
- WILL — Will.
- GRAD — Graduation.
- RETI — Retirement.
- RESI — Residence.
- OCCU — Occupation.
- EDUC — Education.
- RELI — Religious affiliation.
- NATI — Nationality.
- CAST — Caste.
- DSCR — Physical description.
- IDNO — National or other ID number.
- SSN — Social Security Number.
- NCHI — Number of children.
- NMR — Number of marriages.
- PROP — Property/possessions.
- TITL — Title of nobility.
- FACT — Generic attribute/fact.
- EVEN — Generic event.
- ALIA — Alias / alternate-identity pointer.
- ASSO — Association with another individual (e.g. godparent).
- ANCI — Ancestor interest: submitter wants this person's ancestors.
- DESI — Descendant interest: submitter wants this person's descendants.
- RFN — Permanent Record File Number.
- AFN — Ancestral File Number (LDS).
- REFN — User-assigned reference number.
- RIN — Automated record ID (assigned by software).
- CHAN — Change date (last modified).
- RESN — Restriction notice (privacy/lock).

NAME sub-tags

- NPFX — Name prefix (Dr., Sir).
- NICK — Nickname.
- SPFX — Surname prefix (van, de).
- NSFX — Name suffix (Jr., III).
- FONE — Phonetic variant of the name.
- ROMN — Romanized variant of the name.
- TYPE — Name type (birth, married, aka).

Couples asserted only by HUSB/WIFE — dropped (issue #172)

A `FAM` carrying both `HUSB` and `WIFE` but no `MARR` and no shared `CHIL` is
dropped whole: neither spouse keeps a trace of the other, and the family does not
reappear on export. A couple with a `MARR` survives as a spouse edge; a couple
with shared children survives because the exporter regenerates their family from
the co-parent pair. A couple with neither has nothing left to regenerate from.
In bourbon that is 33 of 113 such records — 27 bare `HUSB`+`WIFE`, plus 6 whose
only union evidence is `ENGA`, which GT4 does not model. The losses are not
marginal records: `@F13@` is Charles I of England and Henrietta Maria of France,
a real marriage the source simply never dated, and five of the six `ENGA` ones
are Anne of Brittany's betrothals, which is most of what that record says about
her.

This is not a missing import rule but a missing distinction in the model. GT4 has
one `Spouse` edge with a nullable date, and the exporter writes one `MARR` per
edge — a bare `1 MARR` when the date is null. A dateless spouse edge therefore
already *means* "married, date unknown", and kennedy relies on it: 24 of its 71
couples are exactly that shape. Importing every HUSB+WIFE pair as a spouse edge
would either fabricate a `MARR` for each recovered couple (bourbon's differences
rise from 34 to 79) or, if dateless edges stopped emitting `MARR`, strip the tag
from those 24 kennedy couples. Recovering only the `ENGA` ones trades 6 dropped
couples for 6 invented marriages and moves nothing.

Closing the gap needs a model that separates "married" from "partnered" — a
distinct relationship kind, or a marriage flag on the edge — so the exporter
knows whether to write `MARR`. That was costed and declined: it does clear the
class, but a new kind of relationship runs through kinship, filtering,
statistics, the family tree and both localizations, which is disproportionate to
the records it recovers. Nor can the couple be salvaged from elsewhere in the
file: the exporter already rebuilds a family from any pair sharing a child, and
these records have no child to rebuild from. So the loss stands, pinned by the
round-trip harness (`Tests/Functional/run-gedcom-roundtrip.ps1`), which holds
bourbon to an exact difference count under issue #172.

FAM sub-tags

Preserved verbatim (issue #180): a FAM record is regenerated wholesale from GT4's
edge graph and its xref renumbered, so there is nothing to key its residue by but
the couple itself. Every FAM child below is stored in the project Metadata table
under a `gedcom-family.<id>[.<id>]` key and re-attached to the regenerated record
on export; each `MARR`'s own unmodeled sub-tags (PLAC, SOUR, TYPE, a `Y`
assertion, a DATE GT4 cannot parse) go under that key plus the marriage date,
which is what distinguishes a couple's several marriages in the GT4 model.

A couple can hold only one edge per date, because the `Relatives` primary key
spans the date column — and the column holds the date's code, not its status, so
`1770` and `ABT 1770` are one key too. The first `MARR` of each code keeps the
edge; a later one is preserved whole under the family key instead (issues #188
and #194) and re-emitted after the regenerated `CHIL` links rather than beside
the event it repeats. What that costs is the ordering, not the content — except
that two repeats with byte-identical sub-tags merge into one, since the residue
row folds a root it already carries so a re-import stays a no-op.

The exception is a FAM whose couple survives in no edge at all — the HUSB/WIFE-
only records above, and a lone HUSB or WIFE with no children (kennedy's `@F24@`).
No record is regenerated for those, so there is nothing to merge their residue
back onto and it stays dropped, along with the couple. Closing that needs #172.

Because the key is the couple and the date, editing either in GT4 orphans the row
it addressed: a re-dated marriage re-exports without the PLAC/SOUR it was imported
with. Nothing keyed this way can be stable under edits — the alternative, pairing
residue to marriages by position, misfiles them instead of dropping them.

Two consequences of keying by the couple rather than by the record:

- Several FAM records naming the same couple are one family to GT4, so their
  residue pools onto the single regenerated record — the `NOTE` of one and the
  `CHAN` of another come out together. Nothing is lost; what is lost is the
  statement that the source kept them apart.
- Residue is stored whenever either spouse resolves, whether or not the couple
  ends up in an edge. A FAM of the dropped class above therefore leaves a row
  nothing reads — dead weight in the project rather than a leak, and it would
  start being read if #172 were ever closed.

- DIV — Divorce.
- DIVF — Divorce filed.
- ENGA — Engagement.
- MARB — Marriage banns.
- MARC — Marriage contract.
- MARL — Marriage license.
- MARS — Marriage settlement.
- ANUL — Annulment.
- CENS — Family census.
- EVEN — Generic family event.
- NCHI — Count of children in the family.
- SUBM — Submitter(s) for the family.
- REFN — User-assigned reference number.
- RIN — Automated record ID.
- CHAN — Change date.

Sub-detail tags (under events / names / citations)

- PLAC — Place name.
- MAP — Geographic-coordinates block.
- LATI — Latitude.
- LONG — Longitude.
- ADDR — Address.
- ADR1 / ADR2 / ADR3 — Address lines 1–3.
- CITY — City.
- STAE — State/province.
- POST — Postal code.
- CTRY — Country.
- PHON — Phone number.
- FAX — Fax number.
- EMAIL — Email address.
- WWW — Web address.
- AGE — Age at the event.
- AGNC — Responsible agency/authority.
- CAUS — Cause (e.g. of death).
- TYPE — Qualifier/type of the parent item.
- SOUR — Source citation pointer.
- PAGE — Citation detail (page/entry within the source).
- DATA — Data block inside a source citation.
- EVEN — Event(s) the citation covers.
- ROLE — Role of the person in the cited event.
- QUAY — Quality/certainty of the data.
- OBJE — Multimedia pointer.
- MEDI — Media type of the source.
- CALN — Call number within a repository.
- TIME — Time of day.
- STAT — Status of an event/ordinance.
- TEMP — LDS temple code.
- RELA — Relationship qualifier inside an ASSO link.