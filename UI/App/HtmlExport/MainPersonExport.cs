using GT4.Core.Project.Dto;

namespace GT4.UI.HtmlExport;

// The hidden persons and the saved arrangement live in the project's settings, not its document.
public sealed record MainPersonExport(PersonInfo Person, int[] HiddenIds, IReadOnlyDictionary<int, double> Pins);
