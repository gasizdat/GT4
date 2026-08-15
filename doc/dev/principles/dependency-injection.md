# Principle: dependency injection

GT4 uses `Microsoft.Extensions.DependencyInjection` throughout the UI and Core.Utils layers.
Three rules make it worth documenting beyond "we use DI": how keyed services replace runtime
`switch`-on-category logic, when a class should take typed constructor parameters versus resolve
through `IServiceProvider`, and how a page that MAUI Shell constructs differs from a dialog a page
constructs itself.

## Keyed services replace type-switches, not just factories

Where a family of implementations is chosen by a data-carried key rather than by call site, the
key becomes a DI registration key (`AddKeyedSingleton<TInterface, TImpl>(key)`), not a `switch` or
an `if` chain the caller maintains. `IDataConverter` is the running example: each `DataCategory`
(`PersonPhoto`, `PersonBio`, `PersonAttachment`, …) is registered against its own converter in
`ServiceCollectionExtensions.AddUIUtils` and `GT4Services.Add`
(`UI/Utils/Extensions/ServiceCollectionExtensions.cs`, `UI/App/GT4Services.cs`). A caller never
asks "which converter handles `PersonPhoto`" — it asks the resolver for that category and gets
back whichever converter is registered, or a `FallbackDataConverter`
(`UI/Utils/Converters/FallbackDataConverter.cs`) if none is.

**Never returns null; a resolver is a delegate, not a raw lookup.** `DataConverterResolver`
(`UI/Utils/Converters/DataConverterResolver.cs`) is `IDataConverter (DataCategory category)`,
registered as a factory delegate that wraps `provider.GetKeyedService<IDataConverter>(category)`
with the fallback. Consumers (`InlineMediaProvider`, `PhotoTagDataConverter`, …) take the
*delegate* as a constructor parameter, never `IServiceProvider` itself — they can resolve a
category without knowing DI exists.

**Adding a category means adding a registration, not touching a switch.** When a new
`DataCategory` needs its own conversion behavior, the change is one `AddKeyedSingleton` line where
the others already are — never a new case added to code that inspects the category. If you find
yourself writing `category switch { DataCategory.X => ..., DataCategory.Y => ... }` outside a
converter's own `ToObjectAsync`/`FromObjectAsync`, that's the keyed-service seam being bypassed.

## Typed constructor parameters over service-locator — with one real exception

Prefer declaring a class's actual dependencies as typed constructor parameters over resolving them
via `IServiceProvider.GetRequiredService<T>()` inside the constructor body. The container absorbs
the resolution cost; the constructor signature documents what the class needs.

This is unconditional for classes DI constructs directly — every page under `UI/App/Pages/*.xaml.cs`
is built once by MAUI Shell's container, so its constructor parameters are free.

**It is not unconditional for dialogs**, because dialogs are `new`'d up by a caller at the point a
runtime value (which person, which biological sex) becomes known — something the DI container has
no way to supply. Converting a dialog's constructor to typed parameters is a net win only when its
caller *already* holds those services for its own reasons; otherwise it just relocates the same
`GetRequiredService` calls one frame up, often duplicated across call sites.

**The shape that resolves the tension: a DI-constructed `Factory` record, holding everything DI can
supply, with a `Create(...)` method taking only what DI can't.** `SelectNameDialog.Factory`
(`UI/App/Dialogs/SelectOrCreateName/SelectNameDialog.xaml.cs`) is a record of typed services
(`INameTypeFormatter`, `ICurrentProjectProvider`, `DataConverterResolver`, …), itself registered
with `AddTransient<SelectNameDialog.Factory>()` (`UI/App/GT4Services.cs`); its `Create(BiologicalSex,
NameType[])` supplies only the two values that exist solely at the call site. The same pattern
covers `SelectRelativesDialog`, `SelectPersonDialog`, `SelectMediaDialog`, and
`CreateOrUpdatePersonDialog`.

## Non-goals

- Not a registration reference — `ServiceCollectionExtensions.AddUIUtils` and `GT4Services.Add` are
  themselves the live list of what's registered; this doc doesn't duplicate it.
- Not a MAUI-lifecycle explainer (why Shell constructs pages when it does). Only the DI-shape
  consequence of that fact is in scope here.
- Not `InlineMediaProvider`'s specific resolution logic — that's
  [rendering-inversion.md](rendering-inversion.md); this doc only covers how it's *constructed*.
