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
- While arranging (`IsArranging`), node taps do nothing.

## State
- `_Center` (a `Person?`) / `_CenterName`: the focal person and its formatted short name.
- `_AncestorGenerations` / `_DescendantGenerations`: how many generations are loaded up/down,
  starting at `InitialGenerations` (2) and growable to `MaxGenerations` (120), `GenerationsPerLoad`
  (3) at a time.
- `_IncludeCollaterals`: toggles siblings/cousins; flipping it triggers a reload.
- `_CanLoadMoreAncestors` / `_CanLoadMoreDescendants`: backing fields for the load-more bindables.
- `_ViewTarget` (Center/Top/Bottom/Dropped): records where the viewport should park after a rebuild.
- `_Pins`: the centre's arrangement, person id → offset from the centre in slots. Replaced, never
  mutated, so a load in flight keeps the set it started with.
- `_IsArranging`: arrange mode (header switch); while on, nodes drag instead of tapping and the
  canvas doesn't pan.
- `_Dropped` / `_LastLayout`: the last dropped node with its release position, and the rendered
  layout a drop is measured against.
- `_PanStartScrollX/Y`: scroll offsets captured at the start of a drag-pan.
- `_ZoomScale`: current zoom factor (`MinZoom` 0.4–`MaxZoom` 2.5, `ZoomStep` 0.25), scales every
  `FamilyTreeLayoutMetrics` dimension before layout and triggers a full `Reload` — except `Margin`,
  which is the `OverlayClearance` gap keeping the tree clear of the pinned "load more"/zoom buttons.
  Those are fixed-size overlays, so that one metric follows the font scale that sizes them, never the
  zoom.
- `_LoadOperationsCount`: reentrant in-flight-load counter backing `LoadInProgress`; the load-more
  buttons disable while any load is running.
- `_NodeCache` / `_ConnectorPool` / `_ThumbnailCache`: retained node views, pooled connector shapes,
  and decoded photo thumbnails, reused across loads (see Render, below).

## Build & render pipeline
1. `SetCenter` resets generation depth to default, loads the centre's arrangement from
   `IFamilyTreeArrangementStore` into `_Pins`, raises title/menu property changes, and calls
   `Reload(ViewTarget.Center)`.
2. `Reload` snapshots the centre, marks a load in progress (`SetLoadInProgress`), and fires
   `LoadAsync` off the UI thread via `SafeTask.Run`.
3. `LoadAsync`:
   - gets a DB cancellation token (`CreateDbCancellationToken`),
   - asks `FamilyTreeProvider.BuildAsync` for the tree at the current depths/collateral setting,
   - scales a copy of `FamilyTreeLayoutMetrics` by `_ZoomScale`,
   - runs `FamilyTreeLayout.Compute` with the scaled metrics to compute node bounds + connectors (a
     stateless, tidy pedigree layout: children centred under their parents, each family drawn once;
     see `FamilyTreeLayout` for the algorithm). Being stateless, a "load more" may move people
     already on screen sideways; their left-to-right order holds, and the viewport stays on the
     centre,
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
- `"ZoomIn"` / `"ZoomOut"` (`OnPageCommand`) step `_ZoomScale` by `ZoomStep` through `SetZoom`, which
  clamps to `[MinZoom, MaxZoom]` and reloads (`ViewTarget.Center`) only when the scale actually
  changed — a full reload, since every node/connector size and the canvas itself depend on the
  scaled metrics.
- The page implements `IZoomablePage`, so while it is the current page the app-wide zoom gesture
  (Android pinch) and hotkeys (Ctrl/Cmd +/-, 0) drive the tree instead of the global font scale.
  A pinch calls `Zoom` continuously, so it paces in the time domain — one `ZoomStep` per
  `ZoomIntervalMs` (300), in the sign of the delta, magnitude ignored. `ResetZoom` is unpaced (a
  discrete command, not a gesture) and returns to `DefaultZoom`.

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
- Horizontally centres the focal column, except after a drop (`ViewTarget.Dropped`): then it
  scrolls by however far the relayout moved the dropped node, so the node stays where it was
  released.
- Vertically parks per `_ViewTarget`: top (0) after loading ancestors, bottom (maxY) after loading
  descendants, otherwise centred on the focal person.
- All scroll targets are clamped to valid extents.

## Drag-to-pan (`OnCanvasPan`)
- A `PanGestureRecognizer` on `Canvas` lets desktop users grab and drag the canvas.
- On `Started` it captures the current scroll offsets; on `Running` it translates the pan delta
  into a scroll offset (subtracting the delta so dragging right reveals left-side content),
  clamped to the canvas edges.
- Off while arranging.

## Arranging
- The Arrange switch sets `IsArranging`; `SyncNodeDrag` gives every node except the centre a
  `PanGestureRecognizer` only while arranging (on touch, a pan recognizer can take the gesture from
  the canvas pan and the tap even when it ignores it). The centre never gets one, since every pin is
  measured from it.
- A drag moves the node by its `TranslationX`. On release, `DropNode` puts it back without saving if
  a load is in flight or the drag is shorter than `MinDragDistance`. Otherwise it pins the node at its
  release offset in slots from the centre, measured with the rendered layout's own metrics and moved
  by `FamilyTreeLayout.NearestClearSlot` to clear the centre and the other pinned nodes in its row.
  The pins are saved through `IFamilyTreeArrangementStore` (per project, per centre) and the tree
  reloads with `ViewTarget.Dropped`.
- `FamilyTreeLayout.Compute` keeps pinned nodes at their offsets. Two pins that end up sharing a slot
  (one was placed while the other was out of the tree) are kept a whole slot apart. Only a pinned node
  moves: free nodes keep their order and move only as far as it takes to clear the pins.
- `IsArranged` (any stored pin) switches the title to its arranged form. "Reset arrangement"
  (enabled by `CanResetArrangement`: arranged and no load running) clears the store, then reloads.
- Under Read-only mode the switch and Reset are hidden.

## Connectors & theming
- Each connector is an individual vector `Path` built by `FamilyTreeConnectorShape.Create` and added
  to the `Connectors` AbsoluteLayout. Each family is drawn the pedigree-chart way, every segment once:
  a line between the partners' centres (hidden behind their photos), one drop from its midpoint, a
  sibship bar with softly rounded outer corners, and a stub to each child. The partner line takes the
  spouse colour for a recorded marriage and the descent colour otherwise, since a GEDCOM import leaves
  a family without a MARR unmarried. A relationship the rows can't hold (pedigree
  collapse) is a dashed loop connector (`FamilyTreeConnector.IsLoop`). Per-shape vector geometry
  (rather than a single canvas-spanning `GraphicsView`) scrolls in lockstep with the nodes, so the connectors
  themselves never allocate one surface larger than the GPU's 16384px max-texture size. Note: the
  page still has a known, unresolved GPU-texture-limit crash on very deep trees from other causes
  (see `FamilyTreePageTests.cs`'s class remarks and the `#if DEBUG` `LoadDeep`/`AutoLoad` diagnostic
  commands in this page, added to reproduce it).
- Corner radius comes from `FamilyTreeLayoutMetrics` (scaled by zoom); colors resolve from app
  resources (`Primary` → parent-child, `Accent` → spouse) via `ThemedColor.Resolve`, which switches
  to the `...Dark` resource key under the dark theme, with hard-coded light-theme fallbacks
  (`#1E4437`, `#8B6F4E`). Node rings resolve the same way. Cached node views and pooled connectors
  are keyed on the resolved theme alongside zoom/centre state, so a theme change forces a rebuild.
