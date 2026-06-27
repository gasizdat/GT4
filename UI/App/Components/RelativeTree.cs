using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
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
  private readonly ObservableCollection<RelativeRow> _Rows = new();

  public RelativeTree(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider)
  {
    _CurrentProjectProvider = currentProjectProvider;
    _CancellationTokenProvider = cancellationTokenProvider;
  }

  public ObservableCollection<RelativeRow> Rows => _Rows;

  /// <summary>Replaces the top-level rows. <paramref name="roots"/> arrives already ordered by the page.</summary>
  public void SetRoots(IEnumerable<RelativeInfo> roots, Date? personBirthDate)
  {
    _Rows.Clear();
    var ordered = roots.ToArray();
    for (var i = 0; i < ordered.Length; i++)
    {
      var isLast = i == ordered.Length - 1;
      _Rows.Add(CreateRow(ordered[i], personBirthDate, depth: 0, isLast, ancestorContinues: []));
    }
  }

  public async Task ToggleAsync(RelativeRow row)
  {
    if (row.IsBusy)
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

    try
    {
      while (true)
      {
        var next = await MainThread.InvokeOnMainThreadAsync(() => _Rows.FirstOrDefault(r => !r.IsExpanded));
        if (next is null)
        {
          break;
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
          await PageAlert.CurrentShell.ShowWarningAsync(UIStrings.AlertTextKinshipUnfoldingAborted);
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
  }

  private RelativeRow CreateRow(
    RelativeInfo relative,
    Date? personBirthDate,
    int depth,
    bool isLast,
    bool[] ancestorContinues)
  {
    RelativeRow? row = null;
    var toggleCommand = new SafeCommand(() => ToggleAsync(row!));
    row = new RelativeRow(relative, personBirthDate, depth, isLast, ancestorContinues, toggleCommand);
    return row;
  }
}
