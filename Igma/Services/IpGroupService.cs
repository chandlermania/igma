using System.Runtime.CompilerServices;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Igma.Data;
using Igma.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Igma.Services;

public class IpGroupService(
    IConfiguration config,
    DefaultAzureCredential credential,
    IpLabelRepository labels,
    IpGroupRepository ipGroups,
    IMemoryCache cache) : IIpGroupService
{
    private readonly IReadOnlyList<string> _excludeSubscriptionIds =
        config.GetSection("Azure:ExcludeSubscriptionIds").Get<string[]>() ?? [];

    public async Task<IReadOnlyList<IpGroupSummary>> GetSummariesAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue("summaries", out IReadOnlyList<IpGroupSummary>? cached) && cached is not null)
            return cached;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var client = new ArmClient(credential);
            var subscriptionIds = await DiscoverSubscriptionIdsAsync(client, cts.Token);

            if (subscriptionIds.Count == 0)
                return BuildSummariesFromDb();

            var descriptionMap = ipGroups.GetDescriptionMap();
            var tasks = subscriptionIds.Select(subId => GetSummariesForSubscriptionAsync(client, subId, descriptionMap, cts.Token));
            var results = await Task.WhenAll(tasks);
            var summaries = results.SelectMany(x => x).ToList();

            cache.Set("summaries", (IReadOnlyList<IpGroupSummary>)summaries, TimeSpan.FromSeconds(60));
            return summaries;
        }
        catch (CredentialUnavailableException)
        {
            return BuildSummariesFromDb();
        }
    }

    private IReadOnlyList<IpGroupSummary> BuildSummariesFromDb() =>
        ipGroups.GetSummaries()
            .Select(s =>
            {
                var id = new ResourceIdentifier(s.AzureId);
                return new IpGroupSummary(
                    IpGroupId: s.AzureId,
                    GroupDbId: (int)s.Id,
                    IpGroupName: id.Name ?? s.AzureId,
                    ResourceGroup: id.ResourceGroupName ?? string.Empty,
                    SubscriptionId: id.SubscriptionId ?? string.Empty,
                    SubscriptionName: s.SubscriptionName,
                    TotalCount: (int)s.TotalCount,
                    LabeledCount: (int)s.LabeledCount,
                    Description: s.Description
                );
            }).ToList();

    public async Task<IReadOnlyList<IpGroupEntry>> GetEntriesAsync(string ipGroupId, CancellationToken ct = default)
    {
        try
        {
            var resourceId = new ResourceIdentifier(ipGroupId);
            var subscriptionId = resourceId.SubscriptionId ?? string.Empty;

            var client = new ArmClient(credential);
            var subscriptionResource = client.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            var subscriptionData = await subscriptionResource.GetAsync(ct);
            var subscriptionName = subscriptionData.Value.Data.DisplayName ?? subscriptionId;

            var groupResource = client.GetIPGroupResource(resourceId);
            var group = await groupResource.GetAsync(cancellationToken: ct);

            var ipAddresses = group.Value.Data.IPAddresses ?? [];
            var groupDbId = ipGroups.GetOrCreate(ipGroupId, subscriptionName);
            labels.SyncIps(ipGroupId, ipAddresses);

            var storedLabels = labels.GetByIpGroup(ipGroupId)
                .ToDictionary(l => l.IpAddress, StringComparer.OrdinalIgnoreCase);

            return ipAddresses.Select(ip =>
            {
                storedLabels.TryGetValue(ip, out var lbl);
                return new IpGroupEntry(
                    IpGroupId: ipGroupId,
                    GroupDbId: groupDbId,
                    IpGroupName: group.Value.Data.Name,
                    ResourceGroup: group.Value.Id.ResourceGroupName ?? string.Empty,
                    SubscriptionId: subscriptionId,
                    SubscriptionName: subscriptionName,
                    IpAddress: ip,
                    Label: lbl?.Label,
                    Notes: lbl?.Notes,
                    LabeledAt: lbl?.UpdatedAt,
                    LabeledBy: lbl?.UpdatedBy
                );
            }).OrderBy(e => e.IpAddress).ToList();
        }
        catch (CredentialUnavailableException)
        {
            return BuildEntriesFromDb(ipGroupId);
        }
    }

    private IReadOnlyList<IpGroupEntry> BuildEntriesFromDb(string ipGroupId)
    {
        var resourceId = new ResourceIdentifier(ipGroupId);
        var subId = resourceId.SubscriptionId ?? string.Empty;
        ipGroups.GetIdMap().TryGetValue(ipGroupId, out var info);

        return labels.GetByIpGroup(ipGroupId)
            .Select(l => new IpGroupEntry(
                IpGroupId: ipGroupId,
                GroupDbId: info?.Id ?? 0,
                IpGroupName: resourceId.Name ?? ipGroupId,
                ResourceGroup: resourceId.ResourceGroupName ?? string.Empty,
                SubscriptionId: subId,
                SubscriptionName: info?.SubscriptionName ?? subId,
                IpAddress: l.IpAddress,
                Label: l.Label,
                Notes: l.Notes,
                LabeledAt: l.UpdatedAt,
                LabeledBy: l.UpdatedBy
            ))
            .OrderBy(e => e.IpAddress)
            .ToList();
    }

    public void InvalidateSummaryCache() => cache.Remove("summaries");

    public Task<IReadOnlyList<IpGroupEntry>> SearchAsync(string query, CancellationToken ct = default)
    {
        var groupIdMap = ipGroups.GetIdMap();
        var results = labels.Search(query)
            .Select(l =>
            {
                var id = new ResourceIdentifier(l.IpGroupId);
                var subId = id.SubscriptionId ?? string.Empty;
                groupIdMap.TryGetValue(l.IpGroupId, out var info);
                return new IpGroupEntry(l.IpGroupId, info?.Id ?? 0, id.Name ?? l.IpGroupId,
                    id.ResourceGroupName ?? string.Empty, subId, info?.SubscriptionName ?? subId,
                    l.IpAddress, l.Label, l.Notes, l.UpdatedAt);
            }).ToList();
        return Task.FromResult<IReadOnlyList<IpGroupEntry>>(results);
    }

    public Task<IReadOnlyList<IpGroupEntry>> GetAllUnlabeledAsync(CancellationToken ct = default)
    {
        var groupIdMap = ipGroups.GetIdMap();
        var unlabeled = labels.GetAllUnlabeled()
            .Select(l =>
            {
                var id = new ResourceIdentifier(l.IpGroupId);
                var subId = id.SubscriptionId ?? string.Empty;
                groupIdMap.TryGetValue(l.IpGroupId, out var info);
                return new IpGroupEntry(l.IpGroupId, info?.Id ?? 0, id.Name ?? l.IpGroupId,
                    id.ResourceGroupName ?? string.Empty, subId, info?.SubscriptionName ?? subId,
                    l.IpAddress, null, null, null);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<IpGroupEntry>>(unlabeled);
    }

    private async Task<IReadOnlyList<string>> DiscoverSubscriptionIdsAsync(ArmClient client, CancellationToken ct)
    {
        var ids = new List<string>();
        await foreach (var sub in client.GetSubscriptions().GetAllAsync(ct))
        {
            var subId = sub.Data.SubscriptionId;
            if (subId is not null &&
                !_excludeSubscriptionIds.Any(e => string.Equals(e, subId, StringComparison.OrdinalIgnoreCase)))
            {
                ids.Add(subId);
            }
        }
        return ids;
    }

    private async Task<IEnumerable<IpGroupSummary>> GetSummariesForSubscriptionAsync(
        ArmClient client, string subscriptionId,
        IReadOnlyDictionary<string, string?> descriptionMap,
        CancellationToken ct)
    {
        var subscriptionResource = client.GetSubscriptionResource(
            new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
        var subscriptionData = await subscriptionResource.GetAsync(ct);
        var subscriptionName = subscriptionData.Value.Data.DisplayName ?? subscriptionId;

        var summaries = new List<IpGroupSummary>();

        await foreach (var group in EnumerateIPGroupsAsync(client, subscriptionId, ct))
        {
            var ipAddresses = group.Data.IPAddresses ?? [];
            var azureId = group.Id.ToString();
            var groupDbId = ipGroups.GetOrCreate(azureId, subscriptionName);
            labels.SyncIps(azureId, ipAddresses);

            var labeledCount = labels.CountLabeled(azureId);

            descriptionMap.TryGetValue(azureId, out var description);

            summaries.Add(new IpGroupSummary(
                IpGroupId: azureId,
                GroupDbId: groupDbId,
                IpGroupName: group.Data.Name,
                ResourceGroup: group.Id.ResourceGroupName ?? string.Empty,
                SubscriptionId: subscriptionId,
                SubscriptionName: subscriptionName,
                TotalCount: ipAddresses.Count,
                LabeledCount: labeledCount,
                Description: description
            ));
        }

        ipGroups.DeleteOrphanedForSubscription(subscriptionId, summaries.Select(s => s.IpGroupId));

        return summaries;
    }

    private async IAsyncEnumerable<IPGroupResource> EnumerateIPGroupsAsync(
        ArmClient client,
        string subscriptionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var subscription = client.GetSubscriptionResource(
            new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
        await foreach (var group in subscription.GetIPGroupsAsync(ct))
            yield return group;
    }
}
