namespace RouteWatch.Core;

public sealed class Shipment
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = "";
    public string BranchCode { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset LastUpdatedUtc { get; set; }
}

public static class ShipmentRules
{
    public static readonly string[] Statuses = ["Pending", "InTransit", "Delivered", "Exception"];
    public static bool ValidBranch(string? branch) => branch is { Length: 6 } &&
        branch.StartsWith("BR-", StringComparison.Ordinal) && branch[3..].All(c => c is >= '0' and <= '9');
}
