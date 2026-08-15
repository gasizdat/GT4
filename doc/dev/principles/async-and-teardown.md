# Principle: async work outlives the objects it touches

Any background work that reaches into the project (a DB read, a photo decode that will end in a
save) can find its target gone before it finishes — Android backgrounds the app and `App` closes
the current project mid-flight (`App.CloseOnDeactivationAsync`, `UI/App/App.xaml.cs`); a page can
be navigated away from while a fetch is still running; a resolver can be swapped while a batch is
in flight (see [rendering-inversion.md](rendering-inversion.md)). The failure this produces —
`ObjectDisposedException` or `ProjectNotOpenedException` from the closed project — is not a bug to
fix at the throw site. It's an expected race that every piece of fire-and-forget project-touching
work has to be written to tolerate.

## The rule

**Fire-and-forget work that touches the project goes through `SafeTask`
(`UI/Utils/SafeTask.cs`), never a bare `Task.Run` or a bare `async void`.** `SafeTask.Run` /
`SafeTask.RunOnMainThread` wrap the work and route any exception through
`SafeTask.IsProjectTeardown`: a teardown exception is logged and swallowed, anything else reaches
the caller's `IAlertService`. Without this wrapper, an exception escaping an unobserved `Task` or a
posted continuation is unhandled and terminates the app.

`IsProjectTeardown` recurses into `AggregateException` — several call sites still block on
`Task.Result`, which wraps the real exception — and requires *every* inner exception to qualify
before treating the whole thing as benign, not just one.

**A command that can fail goes through `SafeCommand`/`SafeCommand<T>` (`UI/Utils/SafeCommand.cs`),
never a bare `Command`.** Same shape, applied to the `ICommand` a XAML binding invokes: the
try/catch lives in one place instead of being re-written at every command's execute delegate.

**`async void` is reserved for handlers a framework requires to be `void`** — event handlers
MAUI's `Window.Activated`/`Deactivated`/`Destroying` insist on (`App.ReopenOnActivationAsync`,
`App.CloseOnDeactivationAsync` in `UI/App/App.xaml.cs`), gesture/tap callbacks. Even there, the
handler must catch everything itself — there is no caller to propagate to, and on Android an
escaped exception terminates the process. Anywhere a `Task`-returning method is possible instead,
use one; a `Task` gives a caller something to await, observe, or wrap in `SafeTask`, and a `void`
method gives them nothing.

## Ordering, not just catching

Swallowing the exception is necessary but not sufficient — a close and a reopen racing each other
needs to not interleave in the first place. `App` serializes its own open/close lifecycle behind a
single `SemaphoreSlim` (`App._LifecycleLock`), because MAUI's `Activated`/`Deactivated`/`Destroying`
handlers are themselves fire-and-forget: a quick deactivate/activate could otherwise reopen the
project while the prior close is still flushing to disk. The lock — not the exception handling —
is what keeps that sequence from ever occurring; the exception handling is what makes the *other*
kind of race (project closed mid-read) survivable when the lock doesn't apply.

## Non-goals

- Not a description of `ICurrentProjectProvider`'s open/close mechanics, or of the SQLite
  connection/transaction model those calls sit on top of — see
  [subsystems/core-project.md](../subsystems/core-project.md).
- Not a blanket "always catch exceptions" rule — code with a caller that can meaningfully react to
  a failure should let it propagate. This principle applies specifically to work with no such
  caller: background tasks, lifecycle event handlers, and UI commands.
