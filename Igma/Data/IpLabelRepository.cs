using Dapper;
using Igma.Models;

namespace Igma.Data;

public class IpLabelRepository(IDbConnectionFactory db, ILogger<IpLabelRepository> logger)
{
    private const string JoinSelect = """
        SELECT m.IpGroupId, m.IpAddress, m.CreatedAt,
               a.Label, a.Notes, a.UpdatedBy, a.UpdatedAt
        FROM IpAddresses m
        LEFT JOIN IpAddressLabels a ON a.IpAddress = m.IpAddress
        """;

    public IReadOnlyList<IpLabel> GetByIpGroup(string ipGroupId)
    {
        using var conn = db.CreateConnection();
        return conn.Query<IpLabel>(
            $"{JoinSelect} WHERE m.IpGroupId = @IpGroupId ORDER BY m.IpAddress",
            new { IpGroupId = ipGroupId }).ToList();
    }

    public IpLabel? Get(string ipGroupId, string ipAddress)
    {
        using var conn = db.CreateConnection();
        return conn.QuerySingleOrDefault<IpLabel>(
            $"{JoinSelect} WHERE m.IpGroupId = @IpGroupId AND m.IpAddress = @IpAddress",
            new { IpGroupId = ipGroupId, IpAddress = ipAddress });
    }

    public void Upsert(string ipGroupId, string ipAddress, string? label, string? notes, string? updatedBy = null)
    {
        try
        {
            using var conn = db.CreateConnection();
            using var tx = conn.BeginTransaction();

            // Ensure membership row exists
            conn.Execute("""
                INSERT OR IGNORE INTO IpAddresses (IpGroupId, IpAddress, CreatedAt)
                VALUES (@IpGroupId, @IpAddress, datetime('now'));
                """, new { IpGroupId = ipGroupId, IpAddress = ipAddress }, tx);

            // Upsert the label globally — one row per IP, shared across all groups
            conn.Execute("""
                INSERT INTO IpAddressLabels (IpAddress, Label, Notes, UpdatedBy, CreatedAt, UpdatedAt)
                VALUES (@IpAddress, @Label, @Notes, @UpdatedBy, datetime('now'), datetime('now'))
                ON CONFLICT(IpAddress) DO UPDATE SET
                    Label = @Label, Notes = @Notes, UpdatedBy = @UpdatedBy, UpdatedAt = datetime('now');
                """, new { IpAddress = ipAddress, Label = label, Notes = notes, UpdatedBy = updatedBy }, tx);

            tx.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upsert label for {IpGroupId}/{IpAddress}", ipGroupId, ipAddress);
            throw;
        }
    }

    public int CountLabeled(string ipGroupId)
    {
        using var conn = db.CreateConnection();
        return conn.ExecuteScalar<int>("""
            SELECT COUNT(*)
            FROM IpAddresses m
            JOIN IpAddressLabels a ON a.IpAddress = m.IpAddress
            WHERE m.IpGroupId = @IpGroupId
              AND a.Label IS NOT NULL AND a.Label != ''
            """, new { IpGroupId = ipGroupId });
    }

    public IReadOnlyList<IpLabel> GetAllUnlabeled()
    {
        using var conn = db.CreateConnection();
        return conn.Query<IpLabel>(
            $"{JoinSelect} WHERE a.Label IS NULL OR a.Label = '' ORDER BY m.IpGroupId, m.IpAddress").ToList();
    }

    public IReadOnlyList<IpLabel> Search(string query)
    {
        using var conn = db.CreateConnection();
        var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return conn.Query<IpLabel>(
            $"""
            {JoinSelect}
            WHERE m.IpAddress LIKE @Pattern ESCAPE '\'
               OR a.Label     LIKE @Pattern ESCAPE '\'
               OR a.Notes     LIKE @Pattern ESCAPE '\'
            ORDER BY m.IpGroupId, m.IpAddress
            """,
            new { Pattern = $"%{escaped}%" }).ToList();
    }

    public void SyncIps(string ipGroupId, IList<string> currentIps)
    {
        try
        {
            using var conn = db.CreateConnection();
            using var tx = conn.BeginTransaction();

            if (currentIps.Count == 0)
            {
                conn.Execute("DELETE FROM IpAddresses WHERE IpGroupId = @IpGroupId",
                    new { IpGroupId = ipGroupId }, tx);
            }
            else
            {
                conn.Execute(
                    "DELETE FROM IpAddresses WHERE IpGroupId = @IpGroupId AND IpAddress NOT IN @Ips",
                    new { IpGroupId = ipGroupId, Ips = currentIps }, tx);
            }

            foreach (var ip in currentIps)
                conn.Execute("""
                    INSERT OR IGNORE INTO IpAddresses (IpGroupId, IpAddress, CreatedAt)
                    VALUES (@IpGroupId, @IpAddress, datetime('now'));
                    """, new { IpGroupId = ipGroupId, IpAddress = ip }, tx);

            tx.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync IPs for {IpGroupId}", ipGroupId);
            throw;
        }
    }

}
