# HTML export design mockup (#463)

Static pages for review before any exporter change. They are hand-assembled from a real export of
`demo.ged`, with every date, relation and label string kept exactly as the formatters produce it, so
the layout is tested against real text lengths. Open them straight from disk, as the exported zip is.

- `index.html`: the project index
- `family-14.html`: a family whose members have photos
- `family-8.html`: a family with none
- `person-10.html`: a person with photos
- `person-6.html`: a person with none

Links to pages outside this set are dead. This folder goes once the templates implement the design.

## What changes from the current export

| Change | Cost in the exporter |
|---|---|
| A header bar carries the breadcrumbs; the page body sits in a centred column | Template only: `document.html` gains the shell, `navigation.html` moves into it |
| Person summary card: main photo as the portrait, a silhouette when there is none; birth and death marked in the app's Birth/Death colours | Template, plus a class slot on `field.html` |
| Relatives, family members and index rows become cards with round thumbnails | C#: the person list is fetched without main photos today |
| Relatives come before the photos, since the portrait already heads the page; photos get a heading | Template and ordering; the heading reuses the existing "Photos" string |
| The family page gets a "Persons" heading | Existing string |
| Index: families as chips; persons grouped by initial, with a jump bar | C#: group the sorted list by the common name's first letter |
| Biography at a readable measure, quotes styled | CSS only |

The palette is the app's `Colors.xaml`, with dark mode through `prefers-color-scheme`. There are no
web fonts and no script, so the site still works offline from `file://`.

## Open question: thumbnail cost

The mockup sizes the originals with CSS and loads them with `loading="lazy"`, so a long index only
fetches the thumbnails scrolled into view. The demo portraits are 250 to 330 px and about 20 KB each.
The alternative is writing downscaled copies alongside the originals, which costs a decode and encode
per photo during export.
