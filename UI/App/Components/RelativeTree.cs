using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using System.Collections.ObjectModel;

namespace GT4.UI.Components;

/// <summary>
/// Flattens the relatives tree into a single <see cref="Rows"/> collection bound to one
/// <c>CollectionView</c>. Expanding a row loads its relatives and inserts them right below it (one level
/// deeper); collapsing removes that contiguous subtree. Keeping the visual tree one level deep is what
/// avoids the native WinUI <c>ArrangeOverride</c> stack overflow that recursive nesting hit.
/// </summary>
public sealed class RelativeTree
{
  private static int _ShowingCancellationWarning;

  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;

  // Every fetched row lives here, regardless of whether the current filter shows it -- AllItems is
  // the structural master Expand/Collapse/SetRoots index into, Items is the filtered view the page
  // binds to. A filter change only touches the Items projection, so it can never disturb an
  // already-expanded subtree.
  private readonly FilteredObservableCollection<RelativeRow> _Rows = new();

  private Func<RelativeInfo, bool> _Predicate = _ => true;
  private bool _IsFilterActive;

  public RelativeTree(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
    _Rows.Filter = (_, row) => row.ShouldShow;
  }

  public ObservableCollection<RelativeRow> Rows => _Rows.Items;

  /// <summary>Replaces the top-level rows. <paramref name="roots"/> arrives already ordered by the
  /// page and unfiltered -- the current filter (see <see cref="SetFilter"/>) decides what's visible.</summary>
  public void SetRoots(IEnumerable<RelativeInfo> roots, Date? personBirthDate)
  {
    var ordered = roots.ToArray();
    var built = new RelativeRow[ordered.Length];
    for (var i = 0; i < ordered.Length; i++)
    {
      var isLast = i == ordered.Length - 1;
      built[i] = CreateRow(ordered[i], personBirthDate, depth: 0, isLast, ancestorContinues: []);
    }
    _Rows.Clear();
    _Rows.AddRange(built);
  }

  /// <summary>Sets which rows are visible and re-applies it immediately. Each row is tested against
  /// its own relative only, independent of ancestors or descendants; a hidden row keeps its expand
  /// state and reappears as-is once it matches again. <paramref name="isActive"/> reflects whether a
  /// filter is configured in the UI at all (e.g. <c>PersonFilterView.IsAnyFilterActive</c>), not
  /// whether <paramref name="predicate"/> currently excludes anything -- see
  /// <see cref="RelativeRow.IsFilterActive"/>.</summary>
  public void SetFilter(bool isActive, Func<RelativeInfo, bool> predicate)
  {
    _IsFilterActive = isActive;
    _Predicate = predicate;
    RecomputeVisibility();
    _Rows.Update();
  }

  private void RecomputeVisibility()
  {
    foreach (var row in _Rows.AllItems)
    {
      row.IsFilterActive = _IsFilterActive;
      row.ShouldShow = _Predicate(row.Relative);
    }
  }

  public async Task ToggleAsync(RelativeRow row)
  {
    if (row.IsBusy || row.Issue == RelativeRowIssueType.Loop)
    {
      return;
    }

    row.IsBusy = true;
    try
    {
      if (row.IsExpanded)
      {
        await MainThread.InvokeOnMainThreadAsync(() => Collapse(row));
      }
      else
      {
        using var token = _CancellationTokenProvider.CreateDbCancellationToken();
        var children = await _CurrentProjectProvider
          .Project
          .RelativesProvider
          .GetRelativeInfosAsync(row.Relative, true, token);
        await MainThread.InvokeOnMainThreadAsync(() => Expand(row, children));
      }
    }
    finally
    {
      row.IsBusy = false;
    }
  }

  public async Task ExpandAllAsync(bool expand)
  {
    if (!expand)
    {
      await MainThread.InvokeOnMainThreadAsync(CollapseAll);
      return;
    }

    // Backend data is expected to be a DAG, but a corrupt GEDCOM import (or a bug upstream) can
    // still produce an actual relationship cycle -- a person listed as their own ancestor or
    // descendant. Without this guard, expanding such a person keeps re-inserting the same subtree
    // forever. IsMultipleConnectionsOf tells that apart from a person legitimately appearing twice
    // via two different branches (e.g. cousin marriage sharing a great-grandparent).
    var visitedIds = new Dictionary<int, RelativeInfo>();

    try
    {
      while (true)
      {
        // Rows already flagged as a loop are permanently excluded here: they never become
        // IsExpanded (ToggleAsync refuses to expand them), so without this they'd be re-selected
        // and re-flagged forever.
        var next = await MainThread.InvokeOnMainThreadAsync(() =>
          _Rows.AllItems.FirstOrDefault(r => !r.IsExpanded && r.Issue != RelativeRowIssueType.Loop));
        if (next is null)
        {
          break;
        }

        if (!visitedIds.TryAdd(next.Relative.Id, next.Relative))
        {
          var earlierAddedRelative = visitedIds[next.Relative.Id];
          if (next.Relative.IsMultipleConnectionsOf(earlierAddedRelative))
          {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
              next.Issue = RelativeRowIssueType.MultipleConnections;
              next.IssueMessage = UIStrings.HintRelativeMultipleConnections;
            });
          }
          else
          {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
              next.Issue = RelativeRowIssueType.Loop;
              next.IssueMessage = UIStrings.HintRelativeLoopDetected;
            });
            continue;
          }
        }

        await ToggleAsync(next);
      }
    }
    catch (OperationCanceledException)
    {
      // Benign: the DB token timed out while many nodes were queued through the connection gate. The
      // remaining nodes stay collapsed; warn once so a deep tree doesn't spam the alert.
      if (Interlocked.Exchange(ref _ShowingCancellationWarning, 1) == 0)
      {
        try
        {
          await _AlertService.ShowWarningAsync(UIStrings.AlertTextKinshipUnfoldingAborted);
        }
        finally
        {
          Interlocked.Exchange(ref _ShowingCancellationWarning, 0);
        }
      }
    }
  }

  private void Expand(RelativeRow row, RelativeInfo[] children)
  {
    var index = _Rows.IndexOf(row);
    if (index < 0)
    {
      return;
    }

    var ordered = children.OrderBy(r => r.BiologicalSex).ToArray();
    var newRows = new RelativeRow[ordered.Length];
    for (var i = 0; i < ordered.Length; i++)
    {
      var isLast = i == ordered.Length - 1;
      bool[] ancestorContinues = [.. row.AncestorContinues, !isLast];
      newRows[i] = CreateRow(ordered[i], row.Relative.BirthDate, row.Depth + 1, isLast, ancestorContinues);
    }
    _Rows.InsertRange(index + 1, newRows);

    row.IsExpanded = true;
  }

  private void Collapse(RelativeRow row)
  {
    var index = _Rows.IndexOf(row);
    if (index < 0)
    {
      return;
    }

    var removeCount = 0;
    while (index + 1 + removeCount < _Rows.AllItems.Count && _Rows.AllItems[index + 1 + removeCount].Depth > row.Depth)
    {
      _Rows.AllItems[index + 1 + removeCount].IsExpanded = false;
      removeCount++;
    }
    if (removeCount > 0)
    {
      _Rows.RemoveRange(index + 1, removeCount);
    }

    row.IsExpanded = false;
  }

  private void CollapseAll()
  {
    var roots = _Rows.AllItems.Where(r => r.Depth == 0).ToArray();
    foreach (var root in roots)
    {
      root.IsExpanded = false;
    }
    _Rows.Clear();
    _Rows.AddRange(roots);
  }

  private RelativeRow CreateRow(
    RelativeInfo relative,
    Date? personBirthDate,
    int depth,
    bool isLast,
    bool[] ancestorContinues)
  {
    RelativeRow? row = null;
    var toggleCommand = new SafeCommand(() => ToggleAsync(row!), _AlertService);
    var shouldShow = _Predicate(relative);
    row = new RelativeRow(relative, personBirthDate, depth, isLast, ancestorContinues, shouldShow, _IsFilterActive, toggleCommand);
    return row;
  }
}
