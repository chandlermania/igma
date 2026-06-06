namespace Igma.Models;

public record IpLabel
{
    public required string IpGroupId { get; init; }
    public required string IpAddress { get; init; }
    public string? Label { get; init; }
    public string? Notes { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
