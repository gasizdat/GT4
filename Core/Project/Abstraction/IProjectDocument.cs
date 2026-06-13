using System.Data;

namespace GT4.Core.Project.Abstraction;

public interface IProjectDocument : IAsyncDisposable, IDisposable
{
  const string MimeType = "application/gt4;storage=sqlite";
  long ProjectRevision { get; }
  ITableData Data { get; }
  IFamilyManager FamilyManager { get; }
  ITableMetadata Metadata { get; }
  ITableNames Names { get; }
  ITablePersonData PersonData { get; }
  IPersonManager PersonManager { get; }
  ITablePersonNames PersonNames { get; }
  ITablePersons Persons { get; }
  ITableRelatives Relatives { get; }
  IRelativesProvider RelativesProvider { get; }
  IFamilyTreeProvider FamilyTreeProvider { get; }

  Task<IDbTransaction> BeginTransactionAsync(CancellationToken token);

  /// <summary>
  /// Creates a command bound to the single underlying connection. Configure it and call one of its
  /// <c>Execute*</c> methods, which serialize access to the connection and bind the command to the
  /// current transaction automatically.
  /// </summary>
  ProjectCommand CreateCommand();

  Task<int> GetLastInsertRowIdAsync(CancellationToken token);
  void UpdateRevision();
}