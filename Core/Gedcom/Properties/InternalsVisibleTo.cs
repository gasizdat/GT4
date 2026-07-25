using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GT4.Core.Gedcom.Tests")]
// The round-trip comparison tool reads GEDCOM with the same parser and name/date rules as import, so it
// cannot drift from what the pipeline actually does.
[assembly: InternalsVisibleTo("GT4.Tools.RelativesCli")]
