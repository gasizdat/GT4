# Subsystem: Core.Project

`Core/Project` (`GT4.Core.Project.csproj`) owns the project document: a single SQLite database
representing one genealogy project, plus the file-level lifecycle around it (open a cache copy,
edit, flush back to the origin on close). Everything above this subsystem — GEDCOM import/export,
the UI — talks to it only through the interfaces in `Core/Project/Abstraction`, never to a concrete
table class or the raw `SqliteConnection`.

## Invariant: one connection, serialized

A `SqliteConnection` services one statement at a time. `ProjectDocument`
(`Core/Project/ProjectDocument.cs`) is the single owner of that connection for the whole project;
every `ProjectCommand` it creates and every transaction it begins goes through one shared
`ConnectionGate` (`Core/Project/ConnectionGate.cs`) — a 1-permit semaphore. Any number of
async-flows may call into `ProjectDocument` concurrently; the gate is what makes that safe rather
than a race.

**Closing is a drain, not a slam.** `ConnectionGate.Close`/`CloseAsync` mark the gate closed, then
*acquire* it — waiting out whatever statement, reader, or transaction is currently holding it —
before the document actually tears down the connection. Anything queued behind the gate wakes with
`ObjectDisposedException` instead of hanging forever. This is the mechanism
[async-and-teardown.md](../principles/async-and-teardown.md) describes from the caller's side: the
exception every fire-and-forget project-touching call has to tolerate is this drain completing
underneath it.

## Invariant: transactions are flow-affine, not connection-wide

A transaction belongs to the `AsyncLocal` async-flow that started it (`ConnectionGate.Current`),
which is the correct "same logical thread" notion across `await`/thread-hops. The **first**
transaction opened on a flow (`ProjectDocument.BeginTransactionAsync`) takes a real
`SqliteTransaction` and holds the connection gate for its *entire* lifetime — no other flow can
touch the connection until it completes. Any transaction opened while one is already active *on
the same flow* becomes a SQLite `SAVEPOINT` nested inside the root instead of a second real
transaction (`NestedTransaction.CreateSavepoint`, `Core/Project/NestedTransaction.cs`).

Consequences that follow directly from this, not separate rules:
- Only one transaction can be active on the connection at a time, system-wide.
- A `NestedTransaction` instance is only ever used by the flow that created it — other flows never
  observe it through the ambient, they simply block on the gate.
- A root commit updates the project's revision counter synchronously, in the same call
  (`NestedTransaction.CommitAsync`) — not awaited past the point of releasing the gate, because an
  `await` there would hand the ambient back on a continuation the caller can't observe. Read the
  method's own comment before changing it; this is a correctness requirement, not a style choice.

## Invariant: the file on disk is a cache, flushed on close

`ProjectHost` (`Core/Project/ProjectHost.cs`) opens a project by copying the origin file to a
local cache and pointing `ProjectDocument` at the cache, not the origin. On dispose, it compares
the revision the project was opened at against the revision after the drain completes; only a
project whose revision actually advanced gets copied back over the origin — an unmodified session
just deletes the cache. The revision check, not a dirty flag, is what decides whether a flush
happens, and it's read *after* the drain (`project.Dispose()`/`DisposeAsync()`) specifically so a
transaction that commits while the drain is in progress still counts.

The async flush retries with backoff and only throws after exhausting its attempts — matching the
Android-backgrounding failure mode `async-and-teardown.md` describes, where the origin can be
briefly inaccessible. The edited cache is left on disk on failure, so data loss means "not yet
copied back," not "gone."

## The two data-access shapes, and why both exist

`ITableData` (`Core/Project/Abstraction/ITableData.cs`) is id-keyed and person-agnostic:
`TryGetDataByIdAsync(int id, ...)` finds a row regardless of which person or family it belongs to.
`ITablePersonData` (`Core/Project/Abstraction/ITablePersonData.cs`) is person-scoped:
`GetPersonDataSetAsync(Person, ...)` materializes everything belonging to one person (or a batch of
persons).

**Use the id-keyed shape when the caller doesn't yet know, or shouldn't need to know, whose data
it's fetching** — `InlineMediaProvider` (see
[rendering-inversion.md](../principles/rendering-inversion.md)) resolves a `media:<id>` link this
way precisely because a biography can reference media belonging to someone else entirely. Use the
person-scoped shape when the caller is building a person's own view and wants everything at once —
it's a batch fetch, not a per-item lookup, and pulling single items through it for an
id-keyed use case would materialize the wrong scope of data.

## Where the domain logic lives

`FamilyManager`, `PersonManager`, `RelativesProvider`, `FamilyTreeProvider`, `KinshipFinder`
(`Core/Project/*.cs`) sit above the `Table*` classes and implement the actual genealogy rules
(family-membership consistency, relative/kinship computation, tree layout data). They're
constructed once per `ProjectDocument` and exposed through its `I*` properties
(`ProjectDocument.FamilyManager`, etc.) — the table classes underneath know how to persist a row,
not what the row means.

## Non-goals

- Not a schema reference — table shapes live in the `Table*.cs` files themselves and change
  independently of this document's invariants.
- Not a description of `ProjectCommand`'s SQLite-parameter binding mechanics — implementation
  detail of executing a statement once the gate is already held.
- Not GEDCOM-specific — see [core-gedcom.md](core-gedcom.md) for how import/export uses this
  subsystem's transactions and id-keyed access.
