using System.Security.Cryptography;
using Cheda.App.Storage.Entities;
using Cheda.Core.Security;
using Microsoft.Maui.Storage;
using SQLite;

namespace Cheda.App.Storage;

/// <summary>
/// Owns the encrypted SQLite connection. Call InitializeAsync() at app startup before
/// any repository access.
///
/// Key selection order (Phase 10):
///   1. PIN-derived key from IDatabaseKeyProvider (set after successful PIN/biometric auth).
///   2. Fallback: random per-install key in SecureStorage (used before PIN is configured).
///
/// The fallback key ensures the app works out of the box without a PIN, and the same key
/// is used consistently until the user configures a PIN. When Phase 11 wires the lock screen,
/// Step 1 becomes the active path and the Phase 10 security story is complete.
/// </summary>
public sealed class DatabaseService : IDisposable
{
    private const string FallbackKeyName = "cheda_db_key_v1";

    private readonly IDatabaseKeyProvider _keyProvider;
    private          SQLiteConnection?    _connection;
    private readonly object               _initLock = new();

    public DatabaseService(IDatabaseKeyProvider keyProvider) =>
        _keyProvider = keyProvider;

    public string DbPath { get; } = Path.Combine(
        FileSystem.AppDataDirectory, "cheda.db3");

    public SQLiteConnection Db =>
        _connection ?? throw new InvalidOperationException(
            "DatabaseService not initialized. Call InitializeAsync() after authentication.");

    public async Task InitializeAsync()
    {
        if (_connection is not null) return;

        var key = await ResolveKeyAsync();

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
        // MigrateTable adds new columns to existing tables without dropping data.
        db.CreateTable<LearnedMappingEntity>(CreateFlags.MigrateTable);
        db.CreateTable<RecipientRuleEntity>();
        db.CreateTable<PatternRuleEntity>();
        db.CreateTable<BudgetEntity>();
        db.CreateTable<RecurringBillEntity>();
        db.CreateTable<BillOccurrenceEntity>();
        db.CreateTable<SettingsEntity>();
    }

    /// <summary>
    /// Closes the connection so the database file can be safely copied (backup/restore).
    /// Call ReopenAsync() to resume normal operation.
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

    private async Task<string> ResolveKeyAsync()
    {
        // Prefer a PIN-derived key (set by AppLockService after successful auth).
        var derivedKey = _keyProvider.GetKey();
        if (derivedKey is not null)
            return Convert.ToBase64String(derivedKey);

        // Fallback: random per-install key for installs that have not configured a PIN.
        var stored = await SecureStorage.GetAsync(FallbackKeyName);
        if (stored is not null) return stored;

        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        stored = Convert.ToBase64String(bytes);
        await SecureStorage.SetAsync(FallbackKeyName, stored);
        return stored;
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
