using Cheda.Core.Storage;

namespace Cheda.App.Storage;

/// <summary>
/// Copies the encrypted .db3 file to/from a user-chosen path.
/// The backup file IS the encrypted database — it is unreadable without the key
/// stored in the device's SecureStorage. Key portability (to restore on a new device)
/// will be addressed in Phase 10 with PIN-derived key encryption.
/// </summary>
public sealed class BackupService : IBackupService
{
    private readonly DatabaseService _db;

    public BackupService(DatabaseService db) => _db = db;

    public string DatabasePath => _db.DbPath;

    public async Task ExportAsync(string destinationPath, CancellationToken ct = default)
    {
        // Checkpoint WAL before copy so the .db3 file is fully up to date.
        _db.Db.Execute("PRAGMA wal_checkpoint(FULL)");

        await Task.Run(() =>
        {
            File.Copy(_db.DbPath, destinationPath, overwrite: true);
        }, ct);
    }

    public async Task ImportAsync(string sourcePath, CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Backup file not found.", sourcePath);

        // Close the connection before replacing the file.
        _db.Close();

        try
        {
            await Task.Run(() => File.Copy(sourcePath, _db.DbPath, overwrite: true), ct);
        }
        catch
        {
            // Best-effort reopen even if the copy failed.
            await _db.ReopenAsync();
            throw;
        }

        await _db.ReopenAsync();
    }
}
