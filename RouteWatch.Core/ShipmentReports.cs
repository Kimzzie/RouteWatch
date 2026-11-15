using System.Globalization;
using System.Text;

namespace RouteWatch.Core;

public sealed record ShipmentSummary(int Total, int Pending, int InTransit, int Delivered, int Exception);

public static class ShipmentReports
{
    public static IReadOnlyList<Shipment> Filter(IEnumerable<Shipment> branchShipments, string status) =>
        branchShipments.Where(x => status == "All" || x.Status == status).ToList();

    public static ShipmentSummary Summarize(IEnumerable<Shipment> branchShipments)
    {
        var all = branchShipments.ToList();
        return new(all.Count, all.Count(s => s.Status == "Pending"), all.Count(s => s.Status == "InTransit"),
           all.Count(s => s.Status == "Delivered"), all.Count(s => s.Status == "Exception"));
    }

    private static string Escape(string value) =>
    value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? "\"" + value.Replace("\"", "\"\"") + "\""
        : value;

    public static string Csv(IEnumerable<Shipment> displayed)
    {
        var result = new StringBuilder("TrackingNumber,Origin,Destination,Status,LastUpdatedUtc\r\n");
        foreach (var s in displayed)
            result.AppendJoin(',', Escape(s.TrackingNumber), Escape(s.Origin), Escape(s.Destination), Escape(s.Status),
                Escape(s.LastUpdatedUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture))).Append("\r\n");
        return result.ToString();
    }
}
