using Dapper;

namespace Igma.Data;

public static class DbSeeder
{
    private record SeedGroup(string AzureId, string Name, string ResourceGroup, string SubscriptionName, SeedIp[] IPs);
    private record SeedIp(string Address, string? Label = null, string? Notes = null);

    private static readonly SeedGroup[] Groups =
    [
        new("/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-network/providers/Microsoft.Network/ipGroups/ipg-corp-vpn",
            "ipg-corp-vpn", "rg-network", "Corp-Dev",
        [
            new("10.0.0.1",        "Corp VPN Gateway – Primary",   "Main ingress for employee VPN"),
            new("10.0.0.2",        "Corp VPN Gateway – Secondary", "Failover node"),
            new("10.0.1.0/24",     "Corp VPN Client Pool"),
            new("192.168.10.0/23"),
            new("172.16.5.100"),
        ]),
        new("/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-network/providers/Microsoft.Network/ipGroups/ipg-branch-offices",
            "ipg-branch-offices", "rg-network", "Corp-Dev",
        [
            new("203.0.113.10",    "London Office NAT",    "Managed by Contoso IT"),
            new("203.0.113.20"),
            new("198.51.100.0/28", "Chicago Office NAT"),
            new("198.51.100.16/28"),
            new("10.0.1.0/24",     "Corp VPN Client Pool", "Shared — branch traffic tunnels through VPN pool"),
        ]),
        new("/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-dmz/providers/Microsoft.Network/ipGroups/ipg-external-partners",
            "ipg-external-partners", "rg-dmz", "Corp-Dev",
        [
            new("198.18.0.5"),
            new("198.18.0.6"),
            new("198.18.1.0/24"),
            new("10.0.0.1",        "Corp VPN Gateway – Primary", "Shared — partners route through corp VPN gateway"),
        ]),
        new("/subscriptions/00000000-0000-0000-0000-000000000002/resourceGroups/rg-prod/providers/Microsoft.Network/ipGroups/ipg-prod-services",
            "ipg-prod-services", "rg-prod", "Corp-Prod",
        [
            new("10.100.0.0/16",   "Prod Services Network", "Primary prod VNET range"),
            new("10.100.1.0/24"),
            new("10.100.2.0/24"),
        ]),
        new("/subscriptions/00000000-0000-0000-0000-000000000002/resourceGroups/rg-prod/providers/Microsoft.Network/ipGroups/ipg-corp-vpn",
            "ipg-corp-vpn", "rg-prod", "Corp-Prod",
        [
            new("10.0.0.1",        "Corp VPN Gateway – Primary", "Shared — prod firewall rules reference same gateway"),
            new("10.0.1.0/24",     "Corp VPN Client Pool"),
            new("10.200.0.0/24"),
        ]),
    ];

    public static void Seed(IDbConnectionFactory factory)
    {
        using var conn = factory.CreateConnection();

        var alreadySeeded = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM IpGroups") > 0;
        if (alreadySeeded) return;

        foreach (var group in Groups)
        {
            conn.Execute("""
                INSERT INTO IpGroups (AzureId, SubscriptionName, CreatedAt)
                VALUES (@AzureId, @SubscriptionName, datetime('now'));
                """, new { AzureId = group.AzureId, SubscriptionName = group.SubscriptionName });

            foreach (var ip in group.IPs)
            {
                conn.Execute("""
                    INSERT INTO IpLabels (IpGroupId, IpAddress, Label, Notes, CreatedAt, UpdatedAt)
                    VALUES (@IpGroupId, @IpAddress, @Label, @Notes, datetime('now'), datetime('now'));
                    """,
                    new { IpGroupId = group.AzureId, IpAddress = ip.Address, Label = ip.Label, Notes = ip.Notes });
            }
        }
    }
}
