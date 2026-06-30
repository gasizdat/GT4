# GEDCOM Round-Trip Report — `bourbon.ged`

Import of `bourbon.ged` into a fresh GT4 SQLite project via `GedcomImporter`, re-exported
through `GedcomExporter` to `bourbon-copy.ged`, then compared. The import was run with the
`.ged`'s own folder as the media base path (the realistic GT4 import), which matters for the
photo handling below.

## Headline numbers

| Metric                     | Original   | Copy        |
| -------------------------- | ---------- | ----------- |
| File size                  | 109 KB     | 4.7 MB      |
| Lines                      | 6,216      | 28,255      |
| Individuals (`INDI`)       | 303        | **303** ✓   |
| Families (`FAM`)           | 139        | 89          |
| Spouse couples (`MARR`)    | 34         | **34** ✓    |
| Parent–child edges         | 357        | **all kept**✓ |

Every edge was resolved to person *names* and the sets diffed: **no person, name component,
parent–child link, or marriage is lost.** All residual mismatches trace to the NAME-line
rebuild (row 7), never a dropped relationship.

## Differences — is each expected?

| #  | What differs            | Original → Copy                                                                                                                              | Expected?            | Why                                                                                                                                                                  |
| -- | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1  | **HEAD envelope**       | Rich Ancestris header (`NOTE`, `SUBM` ptr, `SOUR ANCESTRIS`+VERS/CORP/ADDR, `DEST`, `DATE/TIME`, `FILE`, `LANG French`, `PLAC FORM`, `COPR`) → minimal (`SOUR GT4`, `GEDC 5.5.1`, `CHAR UTF-8`) | ✅ Yes                | Exporter writes its own header; source-app header metadata isn't modeled                                                                                            |
| 2  | **Xrefs & ordering**    | `@I1@…`, `@F1@…` source order → `@I{dbId}@` renumbered, sorted by id; `FAM` re-derived & renumbered                                          | ✅ Yes                | IDs reassigned by DB; deterministic output ordering                                                                                                                  |
| 3  | **Photos**              | 49× `OBJE`→`FILE photos/x.jpg` + `TITL` caption → embedded base64 `BLOB` (`FORM`+`_PRIM Y`)                                                  | ✅ Yes                | Media folder supplied, so `photos/*.jpg` were loaded & embedded — **this is the 109 KB→4.7 MB blow-up**. Captions (`TITL`) dropped (no caption field, known deferred item). Without a media path they round-trip verbatim as `FILE` residue with zero size change. |
| 4  | **FAM 139→89**          | 47 "edgeless" families dropped + couple de-dup                                                                                               | ✅ Yes                | A `FAM` with `HUSB`+`WIFE` but **no `MARR` and no children** (betrothal/engagement-only or unmarried childless pairs) encodes no GT4 edge → vanishes. Rest rebuilt one-per-couple. Edge graph fully intact (verified). |
| 5  | **FAM-level events**    | `ENGA`×10, `DIV`×2, and `MARR` sub-tags (`PLAC`/`MAP`/`CAUS`/`NOTE`/`SOUR`), `FAM`-level `CHAN`/`NOTE` → dropped; only `MARR`+`DATE` kept    | ⚠️ Lossy (expected)  | Families are regenerated from the edge graph; only the marriage **date** is modeled. Genuine data loss, within GT4's model.                                          |
| 6  | **Inexact dates**       | 52× `ABT`/`EST`/`CAL`/`BEF`/`AFT`/`BET…AND` on `BIRT`/`DEAT`/`MARR` → `ABT <year>`                                                            | ⚠️ Lossy (expected)  | GT4 has one inexact notion (`YearApproximate`). Note: `FROM..TO` ranges under `OCCU`/`TITL`/`RESI` survive verbatim (they're residue).                               |
| 7  | **NAME value line**     | `Robert /CAPET/ de CLERMONT` → `Robert /CAPET/`; `Louis XIII /CAPET/` → `Louis, XIII /CAPET/`                                                | ✅ Cosmetic           | Value line rebuilt from `GIVN`+`SURN`. Affixes drop from the *string* but **`SPFX`(154), `NSFX`(2), `NICK`(74) all preserved as sub-tags** (counts identical). The comma comes from `GIVN "Louis, XIII"`. |
| 8  | **Unmodeled INDI facts**| `_SOSADABOVILLE`×203, nobility `TITL`×130, `BURI`×18, `OCCU`×11, `EVEN`×12, INDI `CHAN`, `BIRT`/`DEAT` `PLAC`+`MAP`                            | ✅ **Preserved**      | Stored verbatim as residue and merged back — counts match exactly                                                                                                    |
| 9  | **Passthrough records** | `SUBM`×1, `SOUR`×6, `REPO`×4 re-emitted verbatim; inbound references (HEAD `SUBM` ptr, citations) stripped                                    | ✅ Yes                | Whole subtrees kept in Metadata; pointers from modeled records intentionally dropped                                                                                 |
| 10 | **Child ordering**      | Source order → normalized: `NAME, SEX, BIRT, DEAT, NOTE, OBJE, FAMS, FAMC, residue`                                                          | ✅ Cosmetic           | Rebuilt deterministically; no data change                                                                                                                            |
| 11 | **First INDI `NOTE`**   | → biography, re-emitted as `NOTE`; HEAD `NOTE` + `FAM` notes dropped (L1 `NOTE` 11→8)                                                        | ✅ Yes                | First note = bio; header/family notes unmodeled                                                                                                                      |
| 12 | **Encoding**            | UTF-8 **with BOM** → UTF-8 **no BOM**                                                                                                         | ⚠️ Harness artifact  | Bytes are valid UTF-8 (verified `Château`/`Île-de-France` = U+00E2/U+00CE). The missing BOM is the writer's `UTF8Encoding(false)`, not the GEDCOM library.           |

## Verdict

**All differences are expected and explainable**, in three classes:

- **Cosmetic / structural** (rows 1, 2, 7, 10, 12) — reformatting, renumbering, header
  regeneration; no information lost.
- **Faithfully preserved** (rows 3\*, 8, 9) — unmodeled tags survive via residue/passthrough;
  `*`photos convert FILE→BLOB by design.
- **Genuine, bounded losses** (rows 4, 5, 6, and photo captions) — engagement/divorce/
  marriage-place, FAM-level metadata, inexact-date precision, and edgeless family records.
  These are the known gaps in what GT4's data model represents, not round-trip bugs.

No individual, relationship, name part, date (to GT4's precision), or unmodeled custom tag was
dropped.
