GEDCOM tags GT4 does not map into its model

The reader parses every line, so none of these fail an import — they are simply
not turned into GT4 data. Most still round-trip untouched: the "Preserved
verbatim" records below, every unmodeled direct INDI sub-tag, and even the
unmodeled *sub-details* of tags GT4 partially models (e.g. a PLAC or SOUR under
BIRT) are stored as opaque residue and re-emitted on export — GT4 just never
reads their values into its own data model. What is genuinely dropped is whole
top-level OBJE records (and pointers to them), the original HEAD envelope
(regenerated fresh by the exporter), everything under FAM beyond
HUSB/WIFE/CHIL/MARR+DATE (FAM records are regenerated wholesale from the edge
graph, so nothing else about them is preserved, not even as residue), and the
couple itself when a FAM asserts one by HUSB/WIFE alone — see the per-section
notes.

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

- HEAD's own `SUBM` pointer, plus every FAM-level `SOUR` citation — the
  references that point at the preserved records above are not kept there, so
  those records re-export unreferenced. An INDI/event-level `SOUR` citation
  (e.g. under `BIRT`) is different: it is preserved as residue like any other
  unmodeled sub-tag (see "Sub-detail tags" below) and round-trips fine,
  pointer included.

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
knows whether to write `MARR`. Until then the loss is pinned by the round-trip
harness (`Tests/Functional/run-gedcom-roundtrip.ps1`), which holds bourbon to an
exact difference count under issue #172.

FAM sub-tags

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