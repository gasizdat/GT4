# FanChartPage — Logical Overview

`UI/App/Pages/FanChartPage.xaml.cs` is the code-behind for the fan chart: a `ContentPage` that draws
a person's ancestors as rings around them, one generation per ring, on a scrollable, pannable canvas.
It is opened from PersonPage's and FamilyTreePage's menus.

## Entry point & navigation
- Receives its subject via the `PersonInfo` query property (`[QueryProperty]`), whose setter calls
  `SetCenter`.
- A tap resolves through `TapAt`: tapping the **centre** opens their `PersonPage`, tapping any
  **ancestor** (a repeat included) re-centres the chart on them. The `"OpenPerson"` menu command
  opens the centre too.
- Every canvas layer is input-transparent and the canvas carries the tap, pan and pointer
  recognizers. A sector's bounding rectangle overlaps its neighbours', so only
  `FanChartLayout.HitTest`, by radius and angle, can tell which segment a point is in.

## Hovering
- The pointer resolves through `HoverAt`, like a tap, and lights up the person under it: every
  segment they hold (a repeat and their first slot together) gets an unfilled outline at twice the
  weight, and the centre's ring thickens as on FamilyTreePage. Fills stay as they are. The outline's
  translucent colour lies over the segment's own stroke, so the hovered line reads a shade stronger
  than the plain one. Touch has no hover, so mobile has none of this.
- The outlines go on a `Highlights` layer of their own, between the segments and the labels, and
  `SetHovered` replaces its contents when the hovered person changes. The segments are never
  restyled or raised: at ten generations their layer holds 2,046 shapes, and raising one over its
  neighbours reorders that layer's native children on every hover change.
- `Render` rebuilds every layer, so `ClearCanvas` forgets the hovered person, and the next pointer
  move outlines them on the new chart.

## State
- `_Center` / `_CenterName`: the focal person and their short name.
- `_Generations`: how many rings are loaded, from `InitialGenerations` (4) up to `MaxGenerations` (10),
  `GenerationsPerLoad` (2) at a time. Past 10, a segment is too thin to show or hit at any zoom.
- `_HiddenIds`: the persons hidden from the family tree (`IFamilyTreeHiddenPersonsStore`), read here
  and never written; the chart honours them the way the tree does.
- `_Zoom`: the page's `PageZoom`, shared with FamilyTreePage (see `FamilyTreePage.md`).
- `_SweepSetting`: the keyed `FanChartSweepSetting` (180°, 270° or 360°, saved app-wide). The header
  picker binds to `SweepNames` / `SweepIndex`, and choosing a sweep stores it and rebuilds. Choosing
  the one already shown does neither, since the picker echoes its own update back.
- `IsYearsCalendarVisible`: whether the header marks the labels' years as Gregorian, which it does
  while the keyed `CalendarSetting` is any other calendar; a header too narrow for both wraps the
  mark onto a row above the span picker. The years are always the stored Gregorian
  ones: a bare year can't be converted, since a Hebrew or French Republican year starts in autumn.
  Read once at construction: SettingsPage opens only from pages below this one, so the calendar
  can't change while the chart is open.
- `_LastLayout`: the rendered layout, which taps, hover and the viewport are measured against.
- `_Stroke` / `_CenterView` / `_HoveredId`: the rendered outline colour and centre view, and who the
  pointer is over.

## Build & render pipeline
1. `Reload` snapshots the centre, depth and hidden set and runs `LoadAsync` off the UI thread.
2. `LoadAsync` asks `FamilyTreeProvider.BuildAsync` for ancestors only (no descendants, no
   collaterals), scales `FanChartMetrics` by the zoom (line height by the font scale too, margin by
   the font scale alone, as FamilyTreePage does). The minimum line height follows the font scale
   alone: when zoomed in, a thin ring's labels shrink back toward the unzoomed size instead of
   disappearing. Then it runs `FanChartLayout.Compute`, formats each label (short name, plus
   `ILifeDatesFormatter.ToYearsString` where the segment has room), and loads the centre's photo.
3. `Render` rebuilds the canvas whole: the centre disc, one `FanChartSegmentShape` path per segment
   (repeats dashed, on a muted fill), a rotated `Label` per labelled segment at its ring's line
   height, and a `FamilyTreeNodeView` for the centre. Every old view is disconnected first, to
   release its native resources.
4. "Load more" is offered only while the chart reaches the depth asked for.
5. The viewport centres on the centre person once the canvas has been laid out at its new size
   (`Canvas.SizeChanged`): centring before that clamps the scroll to the old extents.

## Layout (`UI/Utils/FanChart/FanChartLayout.cs`)
- Ahnentafel slots: a slot k's father is 2k and their mother 2k + 1. Parents are assigned by sex,
  so a lone mother stays on the mother's side; a parent of unknown sex, or a second parent of the
  same sex, takes whichever slot is free. With more than two parents (birth and adoptive), which two
  show is decided by id, since the tree's edges don't say which is adoptive.
- Slots fill shallowest first, father's side first. Pedigree collapse draws a person in the first
  slot they reach; any later slot is marked as a repeat, with nothing drawn behind it.
- The fan is symmetric about straight up, the father's half on the left. Text runs along the longer
  side of its segment and is never upside down; it drops the years, then the name, as segments thin.
