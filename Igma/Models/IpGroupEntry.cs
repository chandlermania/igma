namespace Igma.Models;

public record IpGroupEntry(
    string IpGroupId,
    int GroupDbId,
    string IpGroupName,
    string ResourceGroup,
    string SubscriptionId,
    string SubscriptionName,
    string IpAddress,
    string? Label,
    string? Notes,
    DateTime? LabeledAt,
    string? LabeledBy = null
)
{
    public bool IsLabeled => !string.IsNullOrWhiteSpace(Label);
}