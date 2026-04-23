using Dapper;
using Igma.Models;

namespace Igma.Data;

public class IpLabelRepository(IDbConnectionFactory db, ILogger<IpLabelRepository> logger)
{
    public IReadOnlyList<IpLabel> GetByIpGroup(string ipGroupId)
    {
        using var conn = db.CreateConnection();
        return conn.Query<IpLabel>(
            "SELECT * FROM IpLabels WHERE IpGroupId = @IpGroupId ORDER BY IpAddress",
            new { IpGroupId = ipGroupId }).ToList();
    }

    public IpLabel? Get(string ipGroupId, string ipAddress)
    {
        using var conn = db.CreateConnection();
        return conn.QuerySingleOrDefault<IpLabel>(
            "SELECT * FROM IpLabels WHERE IpGroupId = @IpGroupId AND IpAddress = @IpAddress",
            new { IpGroupId = ipGroupId, IpAddress = ipAddress });
    }

    // Labels are per-IP, not per-group. Saving a label propagates it to every group containing the same IP/CIDR.
    public void Upsert(string ipGroupId, string ipAddress, string? label, string? notes, string? updatedBy = null)
    {
        try
        {
            using var conn = db.CreateConnection();
            using var tx = conn.BeginTransaction();

            // Ensure a row exists for this specific group (may not exist yet if sync hasn't run)
            conn.Execute("""
                INSERT OR IGNORE INTO IpLabels (IpGroupId, IpAddress, CreatedAt, UpdatedAt)
                VALUES (@IpGroupId, @IpAddress, datetime('now'), datetime('now'));
                """, new { IpGroupId = ipGroupId, IpAddress = ipAddress }, tx);

            // Apply the label across ALL groups that contain this IP/CIDR
            conn.Execute("""
                UPDATE IpLabels
                SET Label = @Label, Notes = @Notes, UpdatedBy = @UpdatedBy, UpdatedAt = datetime('now')
                WHERE IpAddress = @IpAddress;
                """, new { IpAddress = ipAddress, Label = label, Notes = notes, UpdatedBy = updatedBy }, tx);

            tx.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upsert label for {IpGroupId}/{IpAddress}", ipGroupId, ipAddress);
            throw;
        }
    }

    public void Delete(string ipGroupId, string ipAddress)
    {
        try
        {
            using var conn = db.CreateConnection();
            conn.Execute(
                "DELETE FROM IpLabels WHERE IpGroupId = @IpGroupId AND IpAddress = @IpAddress",
                new { IpGroupId = ipGroupId, IpAddress = ipAddress });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete label for {IpGroupId}/{IpAddress}", ipGroupId, ipAddress);
            throw;
        }
    }

    public IReadOnlyList<IpLabel> GetAllUnlabeled()
    {
        using var conn = db.CreateConnection();
        return conn.Query<IpLabel>(
            "SELECT * FROM IpLabels WHERE Label IS NULL OR Label = '' ORDER BY IpGroupId, IpAddress").ToList();
    }

    public IReadOnlyList<IpLabel> Search(string query)
    {
        using var conn = db.CreateConnection();
        var escaped = query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return conn.Query<IpLabel>(
            """
            SELECT * FROM IpLabels
            WHERE IpAddress LIKE @Pattern ESCAPE '\'
               OR Label LIKE @Pattern ESCAPE '\'
               OR Notes LIKE @Pattern ESCAPE '\'
            ORDER BY IpGroupId, IpAddress
            """,
            new { Pattern = $"%{escaped}%" }).ToList();
    }

    // Atomically removes IPs no longer in Azure and inserts any new ones, within a single transaction.
    public void SyncIps(string ipGroupId, IList<string> currentIps)
    {
        try
        {
            using var conn = db.CreateConnection();
            using var tx = conn.BeginTransaction();

            if (currentIps.Count == 0)
            {
                conn.Execute("DELETE FROM IpLabels WHERE IpGroupId = @IpGroupId",
                    new { IpGroupId = ipGroupId }, tx);
            }
            else
            {
                conn.Execute(
                    "DELETE FROM IpLabels WHERE IpGroupId = @IpGroupId AND IpAddress NOT IN @Ips",
                    new { IpGroupId = ipGroupId, Ips = currentIps }, tx);
            }

            foreach (var ip in currentIps)
                conn.Execute("""
                    INSERT OR IGNORE INTO IpLabels (IpGroupId, IpAddress, CreatedAt, UpdatedAt)
                    VALUES (@IpGroupId, @IpAddress, datetime('now'), datetime('now'));
                    """, new { IpGroupId = ipGroupId, IpAddress = ip }, tx);

            tx.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync IPs for {IpGroupId}", ipGroupId);
            throw;
        }
    }

    public void InsertIfNotExists(string ipGroupId, string ipAddress)
    {
        using var conn = db.CreateConnection();
        conn.Execute("""
            INSERT OR IGNORE INTO IpLabels (IpGroupId, IpAddress, CreatedAt, UpdatedAt)
            VALUES (@IpGroupId, @IpAddress, datetime('now'), datetime('now'));
            """,
            new { IpGroupId = ipGroupId, IpAddress = ipAddress });
    }

    public void DeleteStale(string ipGroupId, IEnumerable<string> currentIpAddresses)
    {
        var current = currentIpAddresses.ToList();
        using var conn = db.CreateConnection();
        if (current.Count == 0)
        {
            conn.Execute("DELETE FROM IpLabels WHERE IpGroupId = @IpGroupId",
                new { IpGroupId = ipGroupId });
            return;
        }
        conn.Execute(
            "DELETE FROM IpLabels WHERE IpGroupId = @IpGroupId AND IpAddress NOT IN @Ips",
            new { IpGroupId = ipGroupId, Ips = current });
    }
}
