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
  private bool _IsExpanded;

  public RelativeRow(
    RelativeInfo relative,
    Date? personBirthDate,
    int depth,
    bool isLast,
    bool[] ancestorContinues,
    ICommand toggleCommand)
  {
    Relative = relative;
    PersonBirthDate = personBirthDate;
    Depth = depth;
    IsLast = isLast;
    AncestorContinues = ancestorContinues;
    ToggleCommand = toggleCommand;
  }

  public RelativeInfo Relative { get; }

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

  public event PropertyChangedEventHandler? PropertyChanged;

  private void OnPropertyChanged([CallerMemberName] string? name = null) =>
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
