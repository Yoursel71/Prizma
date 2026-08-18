namespace ZapretTR.Core.Models;

public sealed class ZapretProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string Badge { get; init; } = "Deneysel";
    public ProfileRisk Risk { get; init; } = ProfileRisk.Medium;
    public bool Recommended { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
}
