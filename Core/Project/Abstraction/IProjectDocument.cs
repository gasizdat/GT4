using Microsoft.Data.Sqlite;
using System.Data;

namespace GT4.Core.Project.Abstraction;

public interface IProjectDocument : IAsyncDisposable, IDisposable
{
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

//  static abstract Task<ProjectDocument> CreateNewAsync(string path, string name, CancellationToken token);
//  static abstract Task<ProjectDocument> OpenAsync(string path, CancellationToken token);
  Task<IDbTransaction> BeginTransactionAsync(CancellationToken token);
  SqliteCommand CreateCommand();
  Task<int> GetLastInsertRowIdAsync(CancellationToken token);
  void UpdateRevision();
}