namespace GT4.Core.Project.Dto;

// Parent is the row this one was expanded from, null for a root.
public sealed record RelativesWalkRow(RelativeInfo Relative, RelativeInfo? Parent, int Depth, RelativeIssue Issue);
