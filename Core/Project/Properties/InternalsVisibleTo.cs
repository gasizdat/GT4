using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GT4.Core.Project.Tests", AllInternalsVisible = true)]
[assembly: InternalsVisibleTo("GT4.Core.Gedcom.Tests", AllInternalsVisible = true)]

// Moq/Castle DynamicProxy generates mock types into this assembly name; without visibility to it,
// Castle can't emit a proxy for any interface with an internal member (e.g. ITableMetadata.SetProjectRevision).
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]