namespace Igma.Models;

public record IpGroupSummary(
    string IpGroupId,
    int GroupDbId,
    string IpGroupName,
    string ResourceGroup,
    string SubscriptionId,
    string SubscriptionName,
    int TotalCount,
    int LabeledCount,
    string? Description = null
)
{
    public int UnlabeledCount => TotalCount - LabeledCount;
}