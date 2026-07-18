namespace GT4.Core.Project.Abstraction;

/// <summary>
/// The connection/transaction slice of a project document. Not disposable on its own: the owning
/// document controls the connection lifetime. Unlike the document's raw SQLite connection, commands
/// created here are serialized through the connection gate.
/// </summary>
public interface IProjectConnection
{
  Task<IProjectTransaction> BeginTransactionAsync(CancellationToken token);

  /// <summary>
  /// Creates a command bound to the single underlying connection. Configure it and call one of its
  /// <c>Execute*</c> methods, which serialize access to the connection and bind the command to the
  /// current transaction automatically.
  /// </summary>
  ProjectCommand CreateCommand();
}
