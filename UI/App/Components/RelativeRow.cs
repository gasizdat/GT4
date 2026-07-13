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

  public RelativeRow(
    RelativeInfo relative,
    RelativeInfo rootRelative,
    Date? personBirthDate,
    int depth,
    bool isLast,
    bool[] ancestorContinues,
    ICommand toggleCommand)
  {
    Relative = relative;
    RootRelative = rootRelative;
    PersonBirthDate = personBirthDate;
    Depth = depth;
    IsLast = isLast;
    AncestorContinues = ancestorContinues;
    ToggleCommand = toggleCommand;
  }

  public RelativeInfo Relative { get; }

  /// <summary>The depth-0 ancestor this row descends from (itself, when <see cref="Depth"/> is 0).
  /// Lets the relatives filter test one row and decide visibility for its whole subtree without
  /// walking the tree.</summary>
  public RelativeInfo RootRelative { get; }

  /// <summary>Birth date of the tree-parent this row was expanded from; drives the Parent date rule.</summary>
  public Date? PersonBirthDate { get; }

  public int Depth { get; }

  public bool IsLast { get; }

  /// <summary>
  /// Length <see cref="Depth"/>. For each column <c>k</c>, whether that column's sibling trunk runs on
  /// past this row. The last entry is this row's own column (<c>== !IsLast</c>); earlier entries are
  /// inherited ancestor trunks.
  /// </summary>
  public bool[] AncestorContinues { get; }

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
