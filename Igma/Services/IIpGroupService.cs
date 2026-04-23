using Igma.Models;

namespace Igma.Services;

public interface IIpGroupService
{
    Task<IReadOnlyList<IpGroupSummary>> GetSummariesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IpGroupEntry>> GetEntriesAsync(string ipGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<IpGroupEntry>> GetAllUnlabeledAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IpGroupEntry>> SearchAsync(string query, CancellationToken ct = default);
    void InvalidateSummaryCache();
}
