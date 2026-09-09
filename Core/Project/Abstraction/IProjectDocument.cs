namespace GT4.Core.Project.Abstraction;

public interface IProjectDocument : IProjectConnection, IAsyncDisposable, IDisposable
{
  const string MimeType = "application/gt4;storage=sqlite";
  const string FileExtension = ".gt4";
  long ProjectRevision { get; }
  ITableData Data { get; }
  IFamilyManager FamilyManager { get; }
  ITableMetadata Metadata { get; }
  ITableNames Names { get; }
  ITablePersonData PersonData { get; }
  ITableNameData NameData { get; }
  IPersonManager PersonManager { get; }
  ITablePersonNames PersonNames { get; }
  ITablePersons Persons { get; }
  ITableRelatives Relatives { get; }
  IRelativesProvider RelativesProvider { get; }
  IFamilyTreeProvider FamilyTreeProvider { get; }
  IKinshipFinder KinshipFinder { get; }

  Task ExportSnapshotAsync(string destinationPath, CancellationToken token);
}
