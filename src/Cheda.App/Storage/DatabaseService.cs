using System.Security.Cryptography;
using Cheda.App.Storage.Entities;
using Microsoft.Maui.Storage;
using SQLite;

namespace Cheda.App.Storage;

/// <summary>
/// Owns the encrypted SQLite connection. Call InitializeAsync() at app startup before
/// any repository access.
///
/// Encryption key: randomly generated per-install, stored in Android SecureStorage.
/// In Phase 10, a PIN-derived key (PBKDF2 + Keystore pepper) will replace the random key.
/// </summary>
public sealed class DatabaseService : IDisposable
{
    private const string KeyStoreName = "cheda_db_key_v1";

    private SQLiteConnection? _connection;
    private readonly object _initLock = new();

    public string DbPath { get; } = Path.Combine(
        Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "cheda.db3");

    public SQLiteConnection Db =>
        _connection ?? throw new InvalidOperationException(
            "DatabaseService not initialized. Call InitializeAsync() at app startup.");

    public async Task InitializeAsync()
    {
        if (_connection is not null) return;

        var key = await GetOrCreateKeyAsync();

        lock (_initLock)
        {
            if (_connection is not null) return;

            SQLitePCL.Batteries_V2.Init();

            var connStr = new SQLiteConnectionString(
                DbPath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
                storeDateTimeAsTicks: true,
                key: key);

            _connection = new SQLiteConnection(connStr);
            CreateSchema(_connection);
        }
    }

    private static void CreateSchema(SQLiteConnection db)
    {
        db.CreateTable<TransactionEntity>();
        db.CreateTable<LearnedMappingEntity>();
        db.CreateTable<RecipientRuleEntity>();
        db.CreateTable<PatternRuleEntity>();
        db.CreateTable<BudgetEntity>();
        db.CreateTable<RecurringBillEntity>();
        db.CreateTable<BillOccurrenceEntity>();
        db.CreateTable<SettingsEntity>();
    }

    /// <summary>
    /// Closes the connection so the database file can be safely copied (backup/restore).
    /// Reopens automatically on next Db access after calling ReopenAsync().
    /// </summary>
    internal void Close()
    {
        lock (_initLock)
        {
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
    }

    internal async Task ReopenAsync()
    {
        _connection = null;
        await InitializeAsync();
    }

    private static async Task<string> GetOrCreateKeyAsync()
    {
        var key = await SecureStorage.GetAsync(KeyStoreName);
        if (key is not null) return key;

        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        key = Convert.ToBase64String(bytes);
        await SecureStorage.SetAsync(KeyStoreName, key);
        return key;
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
