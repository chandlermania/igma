using Dapper;

namespace Igma.Data;

public static class DbInitializer
{
    public static void Initialize(IDbConnectionFactory factory)
    {
        using var conn = factory.CreateConnection();

        // WAL mode reduces write contention under concurrent requests
        conn.Execute("PRAGMA journal_mode=WAL;");

        using var tx = conn.BeginTransaction();
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS IpGroups (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                AzureId          TEXT    NOT NULL UNIQUE,
                SubscriptionName TEXT    NOT NULL DEFAULT '',
                CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS IpLabels (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                IpGroupId   TEXT    NOT NULL,
                IpAddress   TEXT    NOT NULL,
                Label       TEXT,
                Notes       TEXT,
                CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now')),
                UpdatedAt   TEXT    NOT NULL DEFAULT (datetime('now')),
                UNIQUE (IpGroupId, IpAddress)
            );

            CREATE INDEX IF NOT EXISTS IX_IpLabels_IpGroupId ON IpLabels (IpGroupId);
            CREATE INDEX IF NOT EXISTS IX_IpLabels_Unlabeled  ON IpLabels (IpGroupId) WHERE Label IS NULL OR Label = '';
            """, transaction: tx);
        tx.Commit();

        var ipGroupCols = conn.Query<string>("SELECT name FROM pragma_table_info('IpGroups')").ToHashSet();
        if (!ipGroupCols.Contains("SubscriptionName"))
            conn.Execute("ALTER TABLE IpGroups ADD COLUMN SubscriptionName TEXT NOT NULL DEFAULT ''");
        if (!ipGroupCols.Contains("Description"))
            conn.Execute("ALTER TABLE IpGroups ADD COLUMN Description TEXT");

        var ipLabelCols = conn.Query<string>("SELECT name FROM pragma_table_info('IpLabels')").ToHashSet();
        if (!ipLabelCols.Contains("UpdatedBy"))
            conn.Execute("ALTER TABLE IpLabels ADD COLUMN UpdatedBy TEXT");
    }
}
