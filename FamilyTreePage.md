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
- `_Center` / `_CenterName`: the focal person and its formatted short name.
- `_AncestorGenerations` / `_DescendantGenerations`: how many generations are loaded up/down,
  starting at `InitialGenerations` (2) and growable to `MaxGenerations` (12).
- `_IncludeCollaterals`: toggles siblings/cousins; flipping it triggers a reload.
- `_ViewTarget` (Center/Top/Bottom): records where the viewport should park after a rebuild.
- `_PanStartScrollX/Y`: scroll offsets captured at the start of a drag-pan.

## Build & render pipeline
1. `SetCenter` resets generation depth to default, raises title/menu property changes, and calls
   `Reload(ViewTarget.Center)`.
2. `Reload` snapshots the centre and fires `LoadAsync` off the UI thread via `SafeTask.Run`.
3. `LoadAsync`:
   - gets a DB cancellation token,
   - asks `FamilyTreeProvider.BuildAsync` for the tree at the current depths/collateral setting,
   - runs `FamilyTreeLayout.Build` to compute node bounds + connectors,
   - precomputes a node-id → display-name dictionary,
   - marshals back to the main thread to call `Render`.
4. `Render`:
   - clears and rebuilds the `Nodes` AbsoluteLayout, creating a `FamilyTreeNodeView` per node
     (centre node flagged), each with a tap gesture bound to `PageCommand`,
   - positions each view via `AbsoluteLayout.SetLayoutBounds`,
   - hands connectors to `_ConnectorsDrawable` and invalidates the connector canvas,
   - sizes the canvas/connectors to the computed `CanvasSize`,
   - kicks off `PositionViewportAsync`.

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
- `_ConnectorsDrawable` (an `IDrawable`) renders parent-child and spouse lines; its corner radius
  comes from `FamilyTreeLayoutMetrics`, and its colors resolve from app resources (`Primary`,
  `Accent`) via the `GetColor` helper, with hard-coded fallbacks.
