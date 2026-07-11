using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;

namespace GT4.Tools.RelativesCli;

public enum TreeIssueType
{
  None,
  Loop,
  MultipleConnections
}

public sealed record TreeNode(RelativeInfo Info, int Depth, string Path, TreeIssueType Issue);

public sealed record TreeIssue(
  int PersonId,
  string DisplayName,
  string FirstPath,
  Generation FirstGeneration,
  Consanguinity FirstConsanguinity,
  string SecondPath,
  Generation SecondGeneration,
  Consanguinity SecondConsanguinity,
  TreeIssueType Type);

/// <summary>
/// Ports the loop/multiple-connections detection from UI/App/Components/RelativeTree.cs
/// (ExpandAllAsync) so it can run outside the MAUI app: repeatedly expand the first
/// not-yet-expanded, not-yet-flagged-Loop row, inserting its children right after it (same
/// pre-order traversal the UI's flattened Rows collection uses). A person-Id seen twice is
/// classified by <see cref="RelativeInfoExtensions.IsMultipleConnectionsOf"/>, the same rule the UI uses.
/// </summary>
public sealed class TreeWalker(IRelativesProvider relativesProvider)
{
  private sealed class Row
  {
    public required RelativeInfo Info { get; init; }
    public required int Depth { get; init; }
    public required string Path { get; init; }
    public bool Expanded { get; set; }
    public TreeIssueType Issue { get; set; }
  }

  public async Task<(IReadOnlyList<TreeNode> Nodes, IReadOnlyList<TreeIssue> Issues)> WalkAsync(
    RelativeInfo[] roots,
    CancellationToken token)
  {
    var rows = roots.Select(r => new Row { Info = r, Depth = 0, Path = r.DisplayName }).ToList();
    var visited = new Dictionary<int, (RelativeInfo Info, string Path)>();
    var issues = new List<TreeIssue>();

    while (true)
    {
      var next = rows.FirstOrDefault(r => !r.Expanded && r.Issue != TreeIssueType.Loop);
      if (next is null)
      {
        break;
      }

      if (!visited.TryAdd(next.Info.Id, (next.Info, next.Path)))
      {
        var (firstInfo, firstPath) = visited[next.Info.Id];
        var isMultipleConnections = next.Info.IsMultipleConnectionsOf(firstInfo);
        next.Issue = isMultipleConnections ? TreeIssueType.MultipleConnections : TreeIssueType.Loop;
        issues.Add(new TreeIssue(
          next.Info.Id,
          next.Info.DisplayName,
          firstPath,
          firstInfo.Generation,
          firstInfo.Consanguinity,
          next.Path,
          next.Info.Generation,
          next.Info.Consanguinity,
          next.Issue));

        if (next.Issue == TreeIssueType.Loop)
        {
          continue;
        }
      }

      var children = await relativesProvider.GetRelativeInfosAsync(next.Info, false, token);
      var index = rows.IndexOf(next);
      for (var i = 0; i < children.Length; i++)
      {
        var child = children[i];
        rows.Insert(index + 1 + i, new Row
        {
          Info = child,
          Depth = next.Depth + 1,
          Path = $"{next.Path} -> {child.DisplayName} ({child.Type})"
        });
      }
      next.Expanded = true;
    }

    var nodes = rows.Select(r => new TreeNode(r.Info, r.Depth, r.Path, r.Issue)).ToArray();
    return (nodes, issues);
  }
}
