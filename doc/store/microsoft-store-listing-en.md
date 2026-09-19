# Microsoft Store listing — English (en)

Source copy for the Partner Center listing of **Genealogy Tree**
(package identity `gasizdat.GenealogyTree`, see `UI/App/AppCommon.props`).

**[listing-data-en-us.csv](listing-data-en-us.csv) is what actually gets
imported into Partner Center** (Store listings page has a matching
Export/Import pair) — update it alongside this file rather than retyping
fields into the UI by hand. This file stays the rationale layer: why each
claim is worded the way it is, character-limit tracking, and the Play
appendix, none of which the CSV carries. When copy changes, edit the prose
here first, then carry the same wording into the CSV in Partner Center's
plain-text style (see the note below).

**Partner Center's Description field does not render Markdown.** A live
export confirmed it: `###` headings and `**bold**` markers both came back as
literal characters, so the checked-in CSV uses ALL-CAPS section headings and
plain punctuation instead (an em dash becomes a comma or semicolon; a
`**Label**` lead-in becomes `Label,`). Match that style in the CSV even though
this file keeps Markdown for its own readability.

Character limits below are Microsoft's published limits as of this writing —
re-check them in Partner Center before pasting, since Microsoft revises them.

Every claim here is traceable to shipping behaviour; see
[claim-sources.md](claim-sources.md) for the file each one comes from. If a
feature changes, fix that table, this file, and the CSV together.

## Product name

Genealogy Tree

## Short description

*Partner Center allows more, but only the first ~200 characters are shown on a
search result card — so the copy is written to fit that.*

Build your family tree offline, on your own machine. GEDCOM import and export,
photos and documents, a zoomable tree, a kinship finder, statistics and a
family calendar. No account, no cloud.

(191 characters)

## Description (≤10,000 chars)

*4,396 characters once the source line wraps below are unwrapped into single
spaces, counting the `###` and `-` markers. Well inside the 10,000 limit; see
the Play appendix, where it matters.*

**Genealogy Tree keeps your family history where it belongs: on your own
computer.** There is no account to create, no server to sync with and no
subscription. A project is a single file you can copy, back up or move like any
other document — and the app never sends anything about your family anywhere.

**Start in a minute, not an afternoon.** New here? Open the built-in demo tree —
a small, real genealogy of the Brontë family, complete with portraits and
biographies — and click around before you type a single name. Already have
research elsewhere? Import a GEDCOM file and your tree is there.

### See how the family fits together

The family tree screen draws ancestors and descendants around one person, with
spouses kept side by side and connecting lines that never cross more than they
have to. Click any relative to re-centre the tree on them. Pull in more
generations upwards or downwards whenever you want, switch siblings and cousins
on or off, zoom from a whole-tree overview down to a single branch, and drag the
canvas to pan around it.

### A page for every person

Each person gets photos, a biography and attachments:

- **Photos** — a main portrait plus as many additional photos as you like, in a
  full-screen viewer.
- **Biography** — rich text with formatting, headings, lists and links. Drop
  images from the person's own photos straight into the text.
- **Attachments** — scans, certificates, PDFs, audio, anything. They open in
  whichever app you normally use for that file type.
- **Relatives** — an expandable tree of everyone connected to this person,
  walked one branch at a time.

### The tools that save the afternoon

- **Kinship finder** — pick any two people and get the answer ("grand aunt",
  "cousin once removed") *and* the full chain of relatives that connects them.
- **Date calendar** — see who was born, who married and who is remembered on
  any day of the year, with round-number anniversaries flagged as milestones.
- **Statistics** — over twenty measures of your project at a glance: how many
  people and families, men and women, average and longest lifespans, births by
  decade, the most common first names, average children per parent, and where
  your data is thin (missing birth dates, people with no photo, people with no
  relatives at all).
- **Filters** — narrow any list by name (with `*` and `?` wildcards), by sex or
  by year.
- **Gallery** — every photo and attachment in the project in one place, each
  one linked back to the person or family it belongs to.
- **Names** — one person can carry several names: first, patronymic, last and
  family. Edit them project-wide, and choose the order they display in.

### Built for real genealogical data

- **Dates you actually have.** "About 1816" and "unknown" are first-class
  values, not blanks you have to fake.
- **GEDCOM 5.5.1 import and export.** Bring a tree in from other genealogy
  software and take it out again. Tags Genealogy Tree doesn't model natively are
  carried through the round trip rather than silently dropped, and so are family
  photos and attachments. If a file declares an ambiguous character set, the app
  asks which codepage it was written in instead of mangling every name in it.
- **Read dates in your calendar.** View every date in the Gregorian, Julian,
  Hebrew or French Republican calendar — switch anytime, without changing how
  anything is stored.
- **Multiple projects.** Keep separate trees for separate branches, or a
  scratch tree next to the real one.
- **Restore points.** Earlier working copies of a project are kept, so a session
  that ends badly is something you roll back from, not something you lose.

### Yours to read comfortably

Light, dark or follow-the-system theme. Scale every piece of text in the app up
or down with Ctrl + plus / minus / 0 — useful for a laptop screen, essential
for a long evening of reading old handwriting.

Mark one person as your project's main person and the app opens straight to
them next time. Handing a project to someone else just to browse? Read-only
mode hides every editing action behind a single toggle.

Available in **English, Russian, German, Spanish and French**.

### Your data, your device

Genealogy Tree has no account system, no cloud sync, no analytics and no ads.
Everything lives in your Documents folder. The only time the app reaches the
internet at all is when *you* put a web link or a web image into a biography and
then open it.

## Features (≤200 chars each, up to 20 entries)

*20 entries; the longest is 136 characters.*


- Zoomable, pannable family tree of ancestors and descendants, re-centred on any relative with a click
- Load more generations upwards or downwards on demand, with siblings and cousins optional
- Person pages with a main portrait, extra photos, a formatted biography and file attachments
- Kinship finder: the relationship between any two people, plus the full chain of relatives connecting them
- Date calendar of births, wedding anniversaries and remembrances, with milestone years highlighted
- Over twenty project statistics: lifespans, births by decade, common names, and where your data is incomplete
- Project-wide gallery of every photo and attachment, linked back to its person or family
- Filter any list by name with * and ? wildcards, by sex, or by year
- Several names per person — first, patronymic, last, family — in a display order you choose
- Approximate and unknown dates are first-class, and any date can display in the Gregorian, Julian, Hebrew or French Republican calendar
- GEDCOM 5.5.1 import and export that preserves tags the app doesn't model natively, family photos and attachments included
- Ambiguous GEDCOM character sets are resolved by asking, not by guessing
- Export or import a whole project as a single .gt4 file, opening with a double-click once associated on Windows
- Mark a project's main person to open straight to them, or switch to read-only mode to browse without editing
- Multiple independent projects, each a single file in your Documents folder
- Restore points to roll a project back to an earlier state
- Light, dark or system theme, and app-wide text scaling with Ctrl + plus / minus / 0
- Built-in Brontë family demo tree to explore before you start your own
- Interface in English, Russian, German, Spanish and French
- No account, no cloud sync, no subscription, no ads, no analytics

## Search terms

*Partner Center caps these (7 terms at the time of writing, 21 words total) —
verify the current limit and trim from the bottom if needed.*

1. genealogy
2. family tree
3. GEDCOM
4. ancestry
5. family history
6. pedigree chart
7. kinship calculator

## What's new in this version

**New**
- Mark a project's main person — the app opens straight to them next time.
- Read-only mode: hide every editing action behind one toggle, for safely
  browsing a shared or borrowed project.
- Display dates in the Gregorian, Julian, Hebrew or French Republican
  calendar. GEDCOM dates written in the French Republican calendar now import
  correctly too.
- Export or import a whole project as a single .gt4 file; on Windows it opens
  with a double-click once associated.
- Family photos and attachments now survive a GEDCOM export and reimport,
  alongside the tags that already did.
- The kinship finder has a swap button to flip the two selected people.
- Every page now shows which project is open.

**Fixed**
- A rare crash decoding some photo thumbnails on Windows.
- Incomplete or asymmetric kinship results for people connected through a
  spouse's sibling.
- Misaligned columns on the families list.
- A duplicated date line on GEDCOM export.
- A crash opening the photo viewer when every photo was a placeholder.

## Before submitting

- **Package identity is confirmed correct** — name, publisher and publisher
  display name all match Partner Center, verified against the Package Family Name
  hash rather than by eye. Detail in
  [release-and-submission.md](release-and-submission.md).
- **Supported languages will read English only.** The MSIX declares `EN-US`; the
  other four are .NET satellite assemblies the package never advertises. The
  copy's "Available in English, Russian, German, Spanish and French" is still
  true of the app.
- **The gallery screenshot is text-heavy** — every row is a Wikimedia
  attribution line from the demo file's `TITL` tags, so it reads more like a
  credits list than a gallery. See
  [screenshots/README.md](screenshots/README.md) ("Still worth fixing").
- **The home/project-list screen is deliberately not in the screenshot set**
  — see screenshots/README.md ("No home screen in this set").
- **The ".gt4 double-click to open" claim (Features list, What's new) is
  verified** against the real packaged MSIX (`4.0.688.0`, 2026-09-19) — see
  "Known at the time of this release" in
  [release-and-submission.md](release-and-submission.md).

---

## Appendix — Google Play (draft, if and when the Android build ships)

Play's fields are shorter and its assets differ: **app title ≤30 chars**,
**short description ≤80 chars**, **full description ≤4,000 chars**, plus a
mandatory **1024×500 feature graphic** and **portrait phone screenshots** — none
of which the Windows capture pass produces. Treat this as a starting point, not
a ready submission.

**Title:** Genealogy Tree (14 chars)

**Short description:** Your family tree, offline and private. GEDCOM in and
out, photos, kinship tools. (80 chars)

**Full description:** the Description above is 4,396 characters against Play's
4,000 limit, so it no longer fits as-is (it did, barely, before this release's
new features were added) and a section has to be cut, not just trimmed. Play
also renders no rich text, so the `###` and `**` markers have to go and
whatever you put in their place counts too. Two edits are needed regardless:
replace the Ctrl + plus / minus / 0 shortcut with the pinch-to-zoom gesture
Android uses for the same thing, and drop the "on your own computer" framing in
the opening line, which reads oddly on a phone.
