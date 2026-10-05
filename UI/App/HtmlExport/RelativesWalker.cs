using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;

namespace GT4.UI.HtmlExport;

/// <summary>
/// Expands relatives nearest first and lists them in tree order. Each person is expanded once: a repeat
/// is listed, flagged by <see cref="RelativeInfoExtensions.IsMultipleConnectionsOf"/> against its first
/// row, and left unexpanded, even where the app's Expand all would expand it; that keeps a large pedigree
/// finite. Children are ordered by sex as in the app, since order decides which repeat is flagged and
/// where the cap falls.
/// </summary>
public sealed class RelativesWalker(IRelativesProvider relativesProvider)
{
  private sealed record Node(RelativesWalkRow Row)
  {
    public List<Node> Children { get; } = [];
  }

  public async Task<(RelativesWalkRow[] Rows, bool IsTruncated)> WalkAsync(
    RelativeInfo[] roots,
    int maxRows,
    CancellationToken token)
  {
    var firstSightings = new Dictionary<int, RelativeInfo>();
    var pending = new Queue<Node>();
    var count = 0;

    bool TryList(List<Node> siblings, IEnumerable<RelativeInfo> relatives, RelativeInfo? parent, int depth)
    {
      foreach (var relative in relatives)
      {
        if (count == maxRows)
        {
          return false;
        }

        var issue = RelativeIssue.None;
        if (!firstSightings.TryAdd(relative.Id, relative))
        {
          var firstSighting = firstSightings[relative.Id];
          issue = relative.IsMultipleConnectionsOf(firstSighting) ? RelativeIssue.MultipleConnections : RelativeIssue.Loop;
        }
        var row = new RelativesWalkRow(relative, parent, depth, issue);
        var node = new Node(row);
        siblings.Add(node);
        count++;
        if (issue == RelativeIssue.None)
        {
          pending.Enqueue(node);
        }
      }
      return true;
    }

    var rootNodes = new List<Node>();
    var isTruncated = !TryList(rootNodes, roots, parent: null, depth: 0);
    while (!isTruncated && pending.TryDequeue(out var next))
    {
      var relative = next.Row.Relative;
      var children = await relativesProvider.GetRelativeInfosAsync(relative, selectMainPhoto: false, token);
      var ordered = children.OrderBy(child => child.BiologicalSex);
      isTruncated = !TryList(next.Children, ordered, relative, next.Row.Depth + 1);
    }

    var rows = new List<RelativesWalkRow>(count);
    void Flatten(Node node)
    {
      rows.Add(node.Row);
      node.Children.ForEach(Flatten);
    }
    rootNodes.ForEach(Flatten);
    return ([.. rows], isTruncated);
  }
}
