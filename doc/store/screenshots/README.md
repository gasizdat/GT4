# Store screenshots

Eleven PNGs at the app's natural window size (1426×746 as captured; MAUI list
pages don't reflow into extra width, so — unlike earlier shoots — the window is
*not* stretched to 1920×1080: doing that left wide empty margins either side of
list content and made the shots look sparse). Captured from the **Release**
Windows head (`UI/App/AppWinOnly.csproj`), English, 100% text scale, default
name/date formats; light theme except `13`. The data is the bundled Brontë demo
tree (`UI/App/Resources/Raw/demo.ged`) — every portrait in it is a public-domain
Wikimedia image, so nothing here is anyone's private research.

Release, not Debug, matters: `PersonInfoView.CommonName` appends `" (Id: N)"` to
every displayed name under `#if DEBUG`, which would put a stray id after each
name in the shots.

## No home screen in this set

Earlier sets included the project-list ("home") screen, light and dark. As of
the September 2026 reshoot that screen is deliberately dropped: it's an empty
list-or-create screen with nothing to show off, and it happens to be the one
screen carrying real risk during capture — it's the page that lists *every*
project on the machine that took the screenshots, not just the demo tree used
for the rest of the set. Every remaining screenshot is taken from inside the
opened demo project, and each one's header names it ("Families | Brontë Family
(Demo)", etc.) so that's independently checkable from the image itself.

## Which build each shot came from

All eleven are from a single session against this release branch's tip. Unlike
the previous set, no shot in this set displays the app version (that only ever
appeared on the now-dropped home screen), so there's no version-pinning
constraint on which commit they're built from.

## Suggested Partner Center order and captions

Microsoft Store takes up to 10 screenshots per device family, so the last one
below is a spare.

| # | File | Caption |
|---|------|---------|
| 1 | `06-family-tree.png` | Ancestors and descendants around one person — click any relative to re-centre |
| 2 | `04-person.png` | Every person gets portraits, life dates and their whole circle of relatives |
| 3 | `05-biography.png` | Write a real biography: headings, lists, emphasis and links |
| 4 | `09-kinship-finder.png` | How are these two related? The answer, and the chain that connects them |
| 5 | `10-date-calendar.png` | Births, anniversaries and remembrances on any day of the year |
| 6 | `07-statistics.png` | Twenty-plus measures of your tree, including where the data is thin |
| 7 | `02-families.png` | Families at a glance, each with the people in it |
| 8 | `08-gallery.png` | Every photo and document in the project, linked to who it belongs to |
| 9 | `13-family-tree-dark.png` | Light, dark, or follow the system |
| 10 | `11-settings.png` | Text size, theme, calendar and date formats are yours to set |
| — | `03-family.png` | *(spare)* One family's members with their life spans |

## Choices behind these shots

Two of them are not what the app offers by default, and a re-shoot that skips
them comes out visibly worse:

- **`06-family-tree.png` and `13-family-tree-dark.png` have the *Relatives*
  toggle on.** Off — the default — Charlotte's tree is her two parents and four
  grandparents, because she had no children, and the lead Store screenshot ends
  up looking like the app cannot draw a family. On, it shows all six Brontë
  siblings and reads like a family tree.
- **`10-date-calendar.png` is April, not the current month.** The calendar opens
  on today, and most months of the demo tree are near-empty. April carries four
  dated entries plus the *"Sometime in April — exact day not recorded"* section,
  which is the only place the month-only-date handling is visible.

`09-kinship-finder.png` pairs Hugh Brunty with his granddaughter Charlotte —
far enough apart to show a connecting chain rather than a one-line answer, and
the demo tree's longest blood relationship. It also shows the swap button
(the ⇅ control next to the two "Choose…" buttons, added since the last shoot).

`11-settings.png` is the unscrolled top of the page (Text size, Background
animation, Theme, with the Calendar section heading visible at the bottom) —
a compromise between showing the settings likely to matter most and hinting at
what's further down; the date-format settings below it aren't visible in this
crop.

## Still worth fixing

- **`08-gallery.png`** is honest but text-heavy: every row is a Wikimedia
  attribution line, because that is what the demo file's `TITL` tags carry. It
  reads more like a credits list than a gallery. Consider dropping it, or
  re-shooting the gallery against a tree with ordinary captions.
- **`04-person.png` catches one of two portraits.** The photo pane cycles, holding
  each for about a second and a half; the shot is on the Armytage engraving, and
  waiting one beat longer gives Branwell's colour painting instead. Either is
  honest — capture during a settled beat, not mid-crossfade, and check the caption
  belongs to the picture under it.

## Reproducing the set

`capture/README.md` has the recipe, the two scripts, and the traps that cost the
most time (synthetic clicks, hover colours, native ComboBox popups that
`PrintWindow` can't see, mouse-wheel scrolling a page). Getting a demo project
onto the machine without exposing the real project list is also covered there
("Getting a demo project without the file picker") — follow it exactly; it's
what keeps this set free of anyone's actual family data.
