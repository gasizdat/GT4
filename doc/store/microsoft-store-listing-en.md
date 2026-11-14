# Microsoft Store listing — English (en)

Source copy for the Partner Center listing of **Genealogy Tree**
(package identity `gasizdat.GenealogyTree`, see `UI/App/AppCommon.props`).

**[listing-data-en-us.csv](listings/listing-data-en-us.csv) is what
actually gets imported into Partner Center**, together with the screenshots
beside it, as one folder import (see release-and-submission.md) — update it
alongside this file rather than retyping fields into the UI by hand. This file stays the rationale layer: why each
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

*About 5,990 characters once the source line wraps below are unwrapped into
single spaces, counting the `###` and `-` markers. Well inside the 10,000
limit; see the Play appendix, where it matters.*

**Genealogy Tree keeps your family history where it belongs: on your own
computer.** There is no account to create, no server to sync with and no
subscription. A project is a single file you can copy, back up or move like any
other document — and the app never sends anything about your family anywhere.

**Start in a minute, not an afternoon.** New here? Open the built-in demo tree —
a small, real genealogy of the Brontë family, complete with portraits and
biographies — and click around before you type a single name. Already have
research elsewhere? Import a GEDCOM file and your tree is there.

### See how the family fits together

The family tree screen draws ancestors and descendants around one person as a
tidy pedigree: children centred under their parents, each family drawn once.
Click any relative to re-centre the tree on them. Pull in more generations
upwards or downwards whenever you want, switch siblings and cousins on or off,
zoom from a whole-tree overview down to a single branch, and drag the canvas to
pan around it. Want it your way? Drag people into place, or right-click someone
to hide them from the tree; the app remembers both for that project.

Want a whole ancestry at a glance? The fan chart puts one person at the centre
and each generation of ancestors in a ring around them, up to ten generations,
drawn as a half, three-quarter or full circle. On either chart, hover over
someone to see a card with their photo, name and life dates.

### A page for every person

Each person gets photos, a biography and attachments:

- **Photos** — a main portrait plus as many additional photos as you like, each
  with its own caption, in a full-screen viewer.
- **Photo enhancement** — while editing a person or a family, enlarge a small
  scan, colorize a black-and-white photo or repair scratches on any photo or
  image attachment. It runs on your own PC, and the result is added as a copy
  beside the original, marked as AI-made in its caption.
- **Biography** — rich text with formatting, headings, lists and links, written
  with a formatting toolbar. Drop photos from the project straight into the
  text at the width you choose, or insert them as plain links.
- **Attachments** — scans, certificates, PDFs, audio, anything, each with its own
  caption. Images show a thumbnail and open in the app's own viewer; everything
  else opens in whichever app you normally use for that file type.
- **Relatives** — an expandable tree of everyone connected to this person,
  walked one branch at a time.

### The tools that save the afternoon

- **Kinship finder** — pick any two people and get the answer ("grand aunt",
  "cousin once removed") *and* the full chain of relatives that connects them.
- **Date calendar** — see who was born, who married and who is remembered on
  any day of the year, with round-number anniversaries flagged as milestones.
- **Date tools** — convert a date between the Gregorian, Julian, Hebrew and
  French Republican calendars, or measure the time between two dates, each
  given in its own calendar.
- **Statistics** — over twenty measures of your project at a glance: how many
  people and families, men and women, average and longest lifespans, births by
  decade, the most common first names, average children per parent, and where
  your data is thin (missing birth dates, people with no photo, people with no
  relatives at all). A single family has its own statistics page, too.
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
- **Share it as a website.** Export the whole project as a self-contained HTML
  site, with a page for every person and family, that opens in any browser
  without the app.
- **Dates in your calendar.** View every date in the Gregorian, Julian,
  Hebrew or French Republican calendar — switch anytime, without changing how
  anything is stored. Found a date in an old record? Enter it in its original
  calendar and the app converts it.
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
mode hides every editing action behind a single toggle. Need a name for a
search engine? Right-click a person's name, dates or biography to copy it.

Available in **English, Russian, German, Spanish and French**.

### Your data, your device

Genealogy Tree has no account system, no cloud sync, no analytics and no ads.
Everything lives in your Documents folder. The only times the app reaches the
internet are when *you* put a web link or a web image into a biography and then
open it, and when you agree to download a photo enhancement model on Windows.
Photos are enhanced on your device and never leave it.

## Features (≤200 chars each, up to 20 entries)

*20 entries; the longest is 184 characters.*


- Zoomable, pannable family tree of ancestors and descendants, re-centred on any relative with a click, more generations on demand
- Drag people into place on the tree or hide them from it, remembered per project
- Fan chart of a person's ancestors, up to ten generations in a half, three-quarter or full circle, re-centred on any ancestor with a click
- Person pages with a main portrait, extra photos, a formatted biography and file attachments, each photo and file captioned
- Photo enhancement on your own PC: enlarge a small scan, colorize a black-and-white photo or repair scratches, saved as a copy beside the original
- Kinship finder: the relationship between any two people, plus the full chain of relatives connecting them
- Date calendar of births, wedding anniversaries and remembrances, with milestone years highlighted
- Date tools: convert a date between the Gregorian, Julian, Hebrew and French Republican calendars, and measure the time between two dates
- Over twenty project statistics: lifespans, births by decade, common names, where your data is incomplete; or statistics for one family alone
- Project-wide gallery of every photo and attachment, linked back to its person or family
- Filter any list by name with * and ? wildcards, by sex, or by year
- Several names per person — first, patronymic, last, family — in a display order you choose
- Approximate and unknown dates are first-class, and dates can be entered and displayed in the Gregorian, Julian, Hebrew or French Republican calendar
- GEDCOM 5.5.1 import and export that preserves tags the app doesn't model natively, family photos and attachments included, and asks about an ambiguous character set instead of guessing
- Export or import a whole project as a single .gt4 file, opening with a double-click once associated on Windows
- Export the whole project as a self-contained HTML website, a page for every person and family
- Mark a project's main person to open straight to them, or switch to read-only mode to browse without editing
- Multiple independent projects, each a single file in your Documents folder, with restore points to roll back to, and a built-in Brontë family demo tree to explore first
- Light, dark or system theme, app-wide text scaling with Ctrl + plus / minus / 0, and an interface in English, Russian, German, Spanish and French
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

*Partner Center caps this field at 1,500 characters; the CSV's plain-text
version is what counts against it.*

**New**
- Enhance photos on your own PC: enlarge a small scan, colorize a
  black-and-white photo or repair scratches. It is offered on every photo and
  image attachment, and the result is saved as a captioned copy beside the
  original. Each step downloads its model once, after asking.
- A fan chart of a person's ancestors: up to ten generations in a half,
  three-quarter or full circle.
- Date tools: convert a date between the Gregorian, Julian, Hebrew and French
  Republican calendars, and measure the time between two dates.
- Enter a date in its original calendar in the date dialog; the app converts
  it.
- Hovering a person on the family tree or the fan chart shows a card with their
  photo, name and life dates, plus their generation (such as Baby Boomers) for
  anyone born since 1883.
- Statistics for a single family, from the family's page.
- Image attachments show a thumbnail, which opens the photo viewer.
- With another calendar chosen, a partial date shows its year in that calendar.

**Fixed**
- Changing an approximate date could drop its "about".
- A GEDCOM export could point a godparent or witness link at the wrong person.
- Rotated phone photos had sideways thumbnails, and the wrong proportions in a
  biography or an exported website.
- Ages and other date spans could be a day or two off around a month's end.
- Copying text could fail while the clipboard was briefly busy.

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
  [listings/README.md](listings/README.md) ("Still worth fixing").
- **The two family-tree screenshots were re-shot for the tidy-pedigree layout**
  (#469). The CSV now names all ten screenshots by path, so the folder import
  uploads them with the text. See listings/README.md ("October 2026 release").
- **The home/project-list screen is deliberately not in the screenshot set**
  — see listings/README.md ("No home screen in this set").
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

**Privacy policy URL** (required; Play takes no pasted text):
`https://gasizdat.github.io/GT4/privacy.html`. Website:
`https://gasizdat.github.io/GT4/`. Check before submitting whether the
declared `READ_MEDIA_IMAGES`/`READ_MEDIA_VIDEO` permissions need a Play
permissions declaration, given the app itself reads only its project files.

**Title:** Genealogy Tree (14 chars)

**Short description:** Your family tree, offline and private. GEDCOM in and
out, photos, kinship tools. (80 chars)

**Full description:** the Description above is about 5,990 characters against
Play's 4,000 limit, so it no longer fits as-is and a section has to be cut, not
just trimmed. Play
also renders no rich text, so the `###` and `**` markers have to go and
whatever you put in their place counts too. Three edits are needed regardless:
replace the Ctrl + plus / minus / 0 shortcut with the pinch-to-zoom gesture
Android uses for the same thing, drop the "on your own computer" framing in
the opening line, which reads oddly on a phone, and drop the right-click
claims (copying text, hiding someone from the tree), which have no Android
gesture yet.
