using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using GT4.UI.Resources;
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

  // The structural master: every fetched row, regardless of whether the current filter shows it.
  // Expand/Collapse/SetRoots only ever touch this one, so a filter change can never disturb an
  // already-expanded subtree.
  private readonly ObservableCollection<RelativeRow> _Rows = new();

  // The view bound by the page: a filtered projection of _Rows, re-derived by ApplyFilter.
  private readonly ObservableCollection<RelativeRow> _VisibleRows = new();

  private Func<RelativeInfo, bool> _FilterPredicate = _ => true;

  public RelativeTree(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    IAlertService alertService)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
    _AlertService = alertService;
  }

  public ObservableCollection<RelativeRow> Rows => _VisibleRows;

  /// <summary>Replaces the top-level rows. <paramref name="roots"/> arrives already ordered by the
  /// page and unfiltered -- the current filter (see <see cref="SetFilter"/>) decides what's visible.</summary>
  public void SetRoots(IEnumerable<RelativeInfo> roots, Date? personBirthDate)
  {
    _Rows.Clear();
    var ordered = roots.ToArray();
    for (var i = 0; i < ordered.Length; i++)
    {
      var isLast = i == ordered.Length - 1;
      _Rows.Add(CreateRow(ordered[i], personBirthDate, depth: 0, isLast, ancestorContinues: []));
    }
    ApplyFilter();
  }

  /// <summary>Sets which roots are visible and re-applies it immediately. A root that stops matching
  /// is merely hidden, not dropped -- its expanded subtree (if any) reappears as-is the moment it
  /// matches again. Only roots are tested; once a root is visible, its already-fetched descendants
  /// show unfiltered.</summary>
  public void SetFilter(Func<RelativeInfo, bool> predicate)
  {
    _FilterPredicate = predicate;
    ApplyFilter();
  }

  /// <summary>Re-derives <see cref="Rows"/> from the structural master and the current filter, with a
  /// minimal insert/remove diff (mirroring <see cref="GT4.UI.Utils.FilteredObservableCollection{T}.Update"/>)
  /// so a recycling CollectionView keeps scroll position and item identity across a refilter.</summary>
  private void ApplyFilter()
  {
    var visible = new List<RelativeRow>(_Rows.Count);
    var rootVisible = true;
    foreach (var row in _Rows)
    {
      if (row.Depth == 0)
      {
        rootVisible = _FilterPredicate(row.Relative);
      }
      if (rootVisible)
      {
        visible.Add(row);
      }
    }

    var visibleSet = new HashSet<RelativeRow>(visible);
    for (var i = _VisibleRows.Count - 1; i >= 0; i--)
    {
      if (!visibleSet.Contains(_VisibleRows[i]))
      {
        _VisibleRows.RemoveAt(i);
      }
    }
    for (var i = 0; i < visible.Count; i++)
    {
      if (i >= _VisibleRows.Count || !ReferenceEquals(_VisibleRows[i], visible[i]))
      {
        _VisibleRows.Insert(i, visible[i]);
      }
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
          _Rows.FirstOrDefault(r => !r.IsExpanded && r.Issue != RelativeRowIssueType.Loop));
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
    for (var i = 0; i < ordered.Length; i++)
    {
      var isLast = i == ordered.Length - 1;
      bool[] ancestorContinues = [.. row.AncestorContinues, !isLast];
      var child = CreateRow(ordered[i], row.Relative.BirthDate, row.Depth + 1, isLast, ancestorContinues);
      _Rows.Insert(index + 1 + i, child);
    }

    row.IsExpanded = true;
    ApplyFilter();
  }

  private void Collapse(RelativeRow row)
  {
    var index = _Rows.IndexOf(row);
    if (index < 0)
    {
      return;
    }

    while (index + 1 < _Rows.Count && _Rows[index + 1].Depth > row.Depth)
    {
      _Rows[index + 1].IsExpanded = false;
      _Rows.RemoveAt(index + 1);
    }

    row.IsExpanded = false;
    ApplyFilter();
  }

  private void CollapseAll()
  {
    for (var i = _Rows.Count - 1; i >= 0; i--)
    {
      if (_Rows[i].Depth > 0)
      {
        _Rows.RemoveAt(i);
      }
      else
      {
        _Rows[i].IsExpanded = false;
      }
    }
    ApplyFilter();
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
    row = new RelativeRow(relative, personBirthDate, depth, isLast, ancestorContinues, toggleCommand);
    return row;
  }
}
