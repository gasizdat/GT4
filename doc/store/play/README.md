# Google Play graphics

Uploaded by hand in Play Console under **Grow users → Store presence → Main
store listing**. Play has no folder import like Partner Center's, so the file
names only fix the order.

## Phone screenshots

Shot on 14 November 2026 on a Samsung SM-A736B (Android 16, 1080 × 2400) running
the Release build of master `f2e6392a` (4.0.826.0), the same code as the
uploaded bundle (version code 855). The app was in English with the light
theme, showing the built-in demo tree, opened fresh in English so the project
name reads "Brontë Family (Demo)".

Each is the screen with the status bar (top 98 px) and the navigation bar cut
off, leaving 1080 × 2160. That is exactly 2:1, Play's limit: the uncropped
1080 × 2400 is 2.22:1 and would be refused.

| File | Shows |
|---|---|
| phone-01-family-tree.png | Family tree centred on Charlotte: parents, grandparents and husband |
| phone-02-person.png | Charlotte's page: dates, captioned portrait, relatives with blood share |
| phone-03-biography.png | Charlotte's biography: Markdown headings, list, quote, and the preserved GEDCOM details |
| phone-04-fan-chart.png | Charlotte's fan chart, 360°, zoomed out one step so every label fits |
| phone-05-kinship-finder.png | Elizabeth Branwell to Emily Jane Brontë: "Niece", with the connecting chain |
| phone-06-date-calendar.png | June: two remembrances, a birth and a wedding anniversary |
| phone-07-statistics.png | Project statistics: overview, ages, births by decade |
| phone-08-families.png | The project's family list with portraits |

None shows the version string (the home page does, which is why it's left out).
Reshoot a screen when its layout changes; to reproduce one, capture with
`adb shell screencap -p` and pull the file (PowerShell's `>` corrupts
`adb exec-out` output), then crop rows 98 to 2257.

## Icon and feature graphic

`icon-512.png` (512 × 512, 32-bit) and `feature-graphic.png` (1024 × 500, no
alpha) are rendered from [source/icon.html](source/icon.html) and
[source/feature.html](source/feature.html), which use the app's own
`appicon.svg` and Open Sans. To redraw one after changing its HTML:

```powershell
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
# a throwaway profile keeps headless Edge from handing off to a running Edge
& $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
  --user-data-dir="$env:TEMP\edge-render" --allow-file-access-from-files --window-size=1024,500 `
  --screenshot="$PWD\feature-graphic.png" "file:///$($PWD -replace '\\','/')/source/feature.html"
```

Edge writes a 24-bit PNG. Play wants the icon 32-bit, so convert `icon-512.png`
after rendering it at 512,512 (for example by redrawing it into a
`Format32bppArgb` bitmap with System.Drawing).
