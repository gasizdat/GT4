using GT4.Core.Project.Dto;

namespace GT4.UI.HtmlExport;

// Parent is the row this one was expanded from, null for a root.
public sealed record RelativesWalkRow(RelativeInfo Relative, RelativeInfo? Parent, int Depth, RelativeIssue Issue);
