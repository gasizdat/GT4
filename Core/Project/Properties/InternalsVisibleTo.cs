using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GT4.Core.Project.Tests", AllInternalsVisible = true)]
[assembly: InternalsVisibleTo("GT4.Core.Gedcom.Tests", AllInternalsVisible = true)]

// Moq/Castle DynamicProxy generates mock types into this assembly name; without visibility to it,
// Castle can't emit a proxy for any interface with an internal member (e.g. ITableMetadata.UpdateProjectRevision).
// AllInternalsVisible is omitted here (unlike the two grants above) because it has no effect either
// way: it's a vestigial knob from old .NET Framework partial-trust/CAS schemes with no type-level
// scoping in modern .NET -- InternalsVisibleTo is assembly-wide regardless of this property.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]