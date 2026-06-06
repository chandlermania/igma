using Dapper;

namespace Igma.Data;

public static class DbInitializer
{
    public static void Initialize(IDbConnectionFactory factory)
    {
        using var conn = factory.CreateConnection();

        // WAL mode reduces write contention under concurrent requests
        conn.Execute("PRAGMA journal_mode=WAL;");

        conn.Execute("""
            CREATE TABLE IF NOT EXISTS IpGroups (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                AzureId          TEXT    NOT NULL UNIQUE,
                SubscriptionName TEXT    NOT NULL DEFAULT '',
                Description      TEXT,
                CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS IpAddresses (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                IpGroupId   TEXT    NOT NULL,
                IpAddress   TEXT    NOT NULL,
                CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now')),
                UNIQUE (IpGroupId, IpAddress)
            );

            CREATE INDEX IF NOT EXISTS IX_IpAddresses_IpGroupId ON IpAddresses (IpGroupId);

            CREATE TABLE IF NOT EXISTS IpAddressLabels (
                IpAddress TEXT PRIMARY KEY,
                Label     TEXT,
                Notes     TEXT,
                UpdatedBy TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """);
    }
}
