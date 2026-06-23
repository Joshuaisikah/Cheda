namespace Cheda.Core.Storage;

public interface IBackupService
{
    string DatabasePath { get; }

    /// <summary>Copies the encrypted database file to the given destination path.</summary>
    Task ExportAsync(string destinationPath, CancellationToken ct = default);

    /// <summary>
    /// Replaces the current database with the backup file at the given source path.
    /// Closes the active connection, copies the file, then reopens.
    /// </summary>
    Task ImportAsync(string sourcePath, CancellationToken ct = default);
}
