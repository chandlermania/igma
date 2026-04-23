using Dapper;

namespace Igma.Data;

public record IpGroupInfo(int Id, string SubscriptionName);

public class IpGroupRepository(IDbConnectionFactory db, ILogger<IpGroupRepository> logger)
{
    public int GetOrCreate(string azureId, string subscriptionName = "")
    {
        try
        {
            using var conn = db.CreateConnection();
            return conn.QuerySingle<int>("""
                INSERT INTO IpGroups (AzureId, SubscriptionName, CreatedAt)
                VALUES (@AzureId, @SubscriptionName, datetime('now'))
                ON CONFLICT (AzureId) DO UPDATE SET
                    SubscriptionName = CASE WHEN excluded.SubscriptionName != '' THEN excluded.SubscriptionName ELSE SubscriptionName END
                RETURNING Id
                """, new { AzureId = azureId, SubscriptionName = subscriptionName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get or create IP Group {AzureId}", azureId);
            throw;
        }
    }

    public string? GetAzureId(int id)
    {
        using var conn = db.CreateConnection();
        return conn.QuerySingleOrDefault<string>(
            "SELECT AzureId FROM IpGroups WHERE Id = @Id",
            new { Id = id });
    }

    public IReadOnlyDictionary<string, IpGroupInfo> GetIdMap()
    {
        using var conn = db.CreateConnection();
        return conn.Query("SELECT Id, AzureId, SubscriptionName FROM IpGroups")
            .ToDictionary(
                r => (string)r.AzureId,
                r => new IpGroupInfo((int)r.Id, (string)r.SubscriptionName));
    }

    public void DeleteOrphanedForSubscription(string subscriptionId, IEnumerable<string> activeAzureIds)
    {
        try
        {
            var active = activeAzureIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            using var conn = db.CreateConnection();
            using var tx = conn.BeginTransaction();

            var stored = conn.Query<string>(
                "SELECT AzureId FROM IpGroups WHERE AzureId LIKE @Prefix",
                new { Prefix = $"/subscriptions/{subscriptionId}/%" }, tx);

            var orphaned = stored.Where(id => !active.Contains(id)).ToList();
            foreach (var azureId in orphaned)
            {
                logger.LogInformation("Removing orphaned IP Group {AzureId}", azureId);
                conn.Execute("DELETE FROM IpLabels WHERE IpGroupId = @AzureId", new { AzureId = azureId }, tx);
                conn.Execute("DELETE FROM IpGroups WHERE AzureId = @AzureId", new { AzureId = azureId }, tx);
            }
            tx.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete orphaned IP Groups for subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public IReadOnlyList<IpGroupDbSummary> GetSummaries()
    {
        using var conn = db.CreateConnection();
        return conn.Query<IpGroupDbSummary>("""
            SELECT g.Id, g.AzureId, g.SubscriptionName,
                   COUNT(l.Id) AS TotalCount,
                   SUM(CASE WHEN l.Label IS NOT NULL AND l.Label != '' THEN 1 ELSE 0 END) AS LabeledCount
            FROM IpGroups g
            LEFT JOIN IpLabels l ON l.IpGroupId = g.AzureId
            GROUP BY g.Id, g.AzureId, g.SubscriptionName
            """).ToList();
    }
}

public record IpGroupDbSummary(long Id, string AzureId, string SubscriptionName, long TotalCount, long LabeledCount);
