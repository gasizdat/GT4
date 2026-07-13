using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace GT4.UI.Components;

/// <summary>
/// A single node of the relatives tree, flattened into <see cref="RelativeTree.Rows"/>. It carries
/// everything a row needs to render itself — indentation <see cref="Depth"/> and the connector-trunk
/// <see cref="AncestorContinues"/> mask — so the recycling <c>CollectionView</c> can never leak one
/// node's visuals onto another. Expand/collapse lives in <see cref="RelativeTree"/>.
/// </summary>
public sealed class RelativeRow : INotifyPropertyChanged
{
  private const string ExpandSymbol = "🔽";
  private const string CollapseSymbol = "­­­­⏫­";
  private const string LoopIcon = "⚠️♾️";
  private const string MultipleConnectionsIcon = "ℹ️";
  private bool _IsExpanded;
  private RelativeRowIssueType _Issue;
  private string? _IssueMessage;

  private bool[] _AncestorVisible;

  public RelativeRow(
    RelativeInfo relative,
    Date? personBirthDate,
    int depth,
    bool isLast,
    bool[] ancestorContinues,
    bool shouldShow,
    bool[] ancestorVisible,
    ICommand toggleCommand,
    RelativeRow? parent = null)
  {
    Relative = relative;
    PersonBirthDate = personBirthDate;
    Depth = depth;
    IsLast = isLast;
    AncestorContinues = ancestorContinues;
    ShouldShow = shouldShow;
    _AncestorVisible = ancestorVisible;
    ToggleCommand = toggleCommand;
    Parent = parent;
  }

  public RelativeInfo Relative { get; }

  /// <summary>Birth date of the tree-parent this row was expanded from; drives the Parent date rule.</summary>
  public Date? PersonBirthDate { get; }

  public int Depth { get; }

  public bool IsLast { get; }

  /// <summary>The row this one was expanded from; null for a top-level root. Purely structural -- lets
  /// <see cref="RelativeTree"/> derive a row's <see cref="AncestorVisible"/> straight from its parent's,
  /// instead of re-deriving the same fact from <see cref="Depth"/> positions in the flattened list.</summary>
  public RelativeRow? Parent { get; }

  /// <summary>
  /// Length <see cref="Depth"/>. For each column <c>k</c>, whether that column's sibling trunk runs on
  /// past this row. The last entry is this row's own column (<c>== !IsLast</c>); earlier entries are
  /// inherited ancestor trunks.
  /// </summary>
  public bool[] AncestorContinues { get; }

  /// <summary>Whether this row's own relative currently matches the active filter -- no cascade in
  /// either direction: a matching ancestor does not pull in a non-matching descendant, and a matching
  /// descendant does not pull in a non-matching ancestor. Recomputed by <see cref="RelativeTree"/>
  /// whenever the filter or the tree structure changes; backs <see cref="RelativeTree.Rows"/>'s
  /// visibility directly.</summary>
  public bool ShouldShow { get; set; }

  /// <summary>
  /// Length <see cref="Depth"/>, parallel to <see cref="AncestorContinues"/>: for each ancestor column
  /// <c>k</c>, whether that ancestor currently has <see cref="ShouldShow"/> set. A non-matching
  /// ancestor stays hidden even when a deeper descendant matches, so its trunk column must not draw --
  /// there is no visible row above to connect it to.
  /// </summary>
  public bool[] AncestorVisible
  {
    get => _AncestorVisible;
    set
    {
      _AncestorVisible = value;
      OnPropertyChanged();
    }
  }

  public ICommand ToggleCommand { get; }

  /// <summary>Guards against a second expand/collapse arriving while the DB load is in flight.</summary>
  public bool IsBusy { get; set; }

  public bool IsExpanded
  {
    get => _IsExpanded;
    set
    {
      if (_IsExpanded != value)
      {
        _IsExpanded = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(MoreBtnName));
      }
    }
  }

  public string MoreBtnName => IsExpanded ? CollapseSymbol : ExpandSymbol;

  /// <summary>Set once by RelativeTree when it detects a loop or a legitimate multiple-connections
  /// case while unfolding this row's ancestry; <see cref="RelativeRowIssueType.None"/> otherwise.</summary>
  public RelativeRowIssueType Issue
  {
    get => _Issue;
    set
    {
      if (_Issue != value)
      {
        _Issue = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(HasIssue));
        OnPropertyChanged(nameof(IssueIcon));
      }
    }
  }

  /// <summary>Human-readable explanation shown next to <see cref="IssueIcon"/> when <see cref="HasIssue"/>.</summary>
  public string? IssueMessage
  {
    get => _IssueMessage;
    set
    {
      if (_IssueMessage != value)
      {
        _IssueMessage = value;
        OnPropertyChanged();
      }
    }
  }

  public bool HasIssue => Issue != RelativeRowIssueType.None;

  public string IssueIcon => Issue switch
  {
    RelativeRowIssueType.Loop => LoopIcon,
    RelativeRowIssueType.MultipleConnections => MultipleConnectionsIcon,
    _ => string.Empty
  };

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
