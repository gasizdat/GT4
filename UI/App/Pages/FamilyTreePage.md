# FamilyTreePage — Logical Overview

`UI/App/Pages/FamilyTreePage.xaml.cs` is the code-behind for the family-tree screen: a
`ContentPage` that renders a scrollable, pannable canvas of person nodes plus the connectors
between them, centred on a focal person.

## Entry point & navigation
- Receives its subject via the `PersonInfo` query property (`[QueryProperty]`), whose setter
  calls `SetCenter`.
- Tapping any node routes through `PageCommand` (`OnPageCommand`):
  - tap on the **current centre** → navigate to that person's `PersonPage`;
  - tap on **any other node** → re-centre the tree on that person (`SetCenter`);
  - the `"OpenPerson"` toolbar command → open the centre in `PersonPage`.

## State
- `_Center` (a `Person?`) / `_CenterName`: the focal person and its formatted short name.
- `_AncestorGenerations` / `_DescendantGenerations`: how many generations are loaded up/down,
  starting at `InitialGenerations` (2) and growable to `MaxGenerations` (120), `GenerationsPerLoad`
  (3) at a time.
- `_IncludeCollaterals`: toggles siblings/cousins; flipping it triggers a reload.
- `_CanLoadMoreAncestors` / `_CanLoadMoreDescendants`: backing fields for the load-more bindables.
- `_ViewTarget` (Center/Top/Bottom): records where the viewport should park after a rebuild.
- `_PanStartScrollX/Y`: scroll offsets captured at the start of a drag-pan.
- `_ZoomScale`: current zoom factor (`MinZoom` 0.4–`MaxZoom` 2.5, `ZoomStep` 0.25), scales every
  `FamilyTreeLayoutMetrics` dimension before layout and triggers a full `Reload`.
- `_LoadOperationsCount`: reentrant in-flight-load counter backing `LoadInProgress`; the load-more
  buttons disable while any load is running.
- `_NodeCache` / `_ConnectorPool` / `_ThumbnailCache`: retained node views, pooled connector shapes,
  and decoded photo thumbnails, reused across loads (see Render, below).

## Build & render pipeline
1. `SetCenter` resets generation depth to default, clears the layout's stored positions
   (`_Layout.Reset()`) so the new centre lays out from scratch, raises title/menu property changes,
   and calls `Reload(ViewTarget.Center)`.
2. `Reload` snapshots the centre, marks a load in progress (`SetLoadInProgress`), and fires
   `LoadAsync` off the UI thread via `SafeTask.Run`.
3. `LoadAsync`:
   - gets a DB cancellation token (`CreateDbCancellationToken`),
   - asks `FamilyTreeProvider.BuildAsync` for the tree at the current depths/collateral setting,
   - scales a copy of `FamilyTreeLayoutMetrics` by `_ZoomScale`,
   - runs `FamilyTreeLayout.Update` with the scaled metrics to compute node bounds + connectors (a
     spring layout with an insertion-based row-reorder pass that keeps spouses adjacent and
     connector lines from overlapping; see `FamilyTreeLayout` for the algorithm),
   - decodes any newly-seen photos into `_ThumbnailCache` (`CacheThumbnails`),
   - precomputes a node-id → display-name dictionary,
   - marshals back to the main thread (`SafeTask.RunOnMainThread`) to call `Render`, then clears
     the in-progress flag (`ResetLoadInProgress`) in a `finally`.
4. `Render` does **not** clear and rebuild the canvas — it reuses what's already on screen:
   - `UpdateConnectors` re-specifies the first N pooled `Path` shapes in place
     (`FamilyTreeConnectorShape.Update`) for the new connector list, creates any extra shapes needed,
     and drops (disconnects) surplus ones. The `Connectors` AbsoluteLayout is a sibling declared
     before `Nodes`, so lines sit behind nodes.
   - `UpdateNodes` keeps each person's `FamilyTreeNodeView` alive across loads (keyed by person id in
     `_NodeCache`); a view is only rebuilt if its zoom or centre-flag changed, otherwise just
     repositioned via `SetLayoutBounds`. Nodes no longer present are removed and disconnected.
   - sizes the canvas to the computed `CanvasSize`,
   - recomputes `CanLoadMoreAncestors`/`CanLoadMoreDescendants` from the returned min/max generation,
   - kicks off `PositionViewportAsync`.
   - `"Refresh"` (`OnPageCommand`) is the exception: it calls `ClearRenderCache` first to drop every
     cached node/connector/thumbnail, so a stale name or photo edited elsewhere is picked up — the
     page never auto-reloads on its own.

## Zoom
- `"ZoomIn"` / `"ZoomOut"` (`OnPageCommand`) step `_ZoomScale` by `ZoomStep`, clamped to
  `[MinZoom, MaxZoom]`, then `Reload(ViewTarget.Center)` — a full reload, since every node/connector
  size and the canvas itself depend on the scaled metrics.

## "Load more" affordances
- `CanLoadMoreAncestors` / `CanLoadMoreDescendants` are bindable bools driving the top/bottom
  load-more buttons.
- They are enabled only when (a) below the `MaxGenerations` ceiling **and** (b) the returned tree
  actually reached the requested depth (computed from min/max `Generation` of returned nodes) — so
  a direction with no more data hides its button.
- `"LoadAncestors"` / `"LoadDescendants"` commands increment the respective depth and reload with
  `ViewTarget.Top` / `ViewTarget.Bottom`; `"Refresh"` reloads centred.

## Viewport positioning (`PositionViewportAsync`)
- Yields once so the ScrollView can measure new content first.
- Always horizontally centres the focal column.
- Vertically parks per `_ViewTarget`: top (0) after loading ancestors, bottom (maxY) after loading
  descendants, otherwise centred on the focal person.
- All scroll targets are clamped to valid extents.

## Drag-to-pan (`OnCanvasPan`)
- A `PanGestureRecognizer` on `Canvas` lets desktop users grab and drag the canvas.
- On `Started` it captures the current scroll offsets; on `Running` it translates the pan delta
  into a scroll offset (subtracting the delta so dragging right reveals left-side content),
  clamped to the canvas edges.

## Connectors & theming
- Each connector is an individual vector `Path` built by `FamilyTreeConnectorShape.Create` and added
  to the `Connectors` AbsoluteLayout: parent-child links are orthogonal lines with softly rounded
  right-angle bends, spouse links straight horizontal lines. Per-shape vector geometry (rather than a
  single canvas-spanning `GraphicsView`) scrolls in lockstep with the nodes, so the connectors
  themselves never allocate one surface larger than the GPU's 16384px max-texture size. Note: the
  page still has a known, unresolved GPU-texture-limit crash on very deep trees from other causes
  (see `FamilyTreePageTests.cs`'s class remarks and the `#if DEBUG` `LoadDeep`/`AutoLoad` diagnostic
  commands in this page, added to reproduce it).
- Corner radius comes from `FamilyTreeLayoutMetrics` (scaled by zoom); colors resolve from app
  resources (`Primary` → parent-child, `Accent` → spouse) via the `GetColor` helper, with hard-coded
  fallbacks (`#1E4437`, `#8B6F4E`).
