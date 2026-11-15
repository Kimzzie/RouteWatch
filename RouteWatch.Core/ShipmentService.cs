using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RouteWatch.Core;

public sealed class ShipmentService(HttpClient http, DbContextOptions<ShipmentDb> database)
{
    private sealed record ApiShipment(string? TrackingNumber, string? BranchCode, string? Origin,
        string? Destination, string? Status, DateTimeOffset? LastUpdatedUtc);
    private sealed record ApiResponse(string? BranchCode, ApiShipment[]? Shipments);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = new ShipmentDb(database);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> GetBranchAsync(string branch, CancellationToken cancellationToken = default)
    {
        if (!ShipmentRules.ValidBranch(branch)) throw new ArgumentException("Enter a branch such as BR-001.");
        await using var db = new ShipmentDb(database);
        return await db.Shipments.AsNoTracking()
            .OrderBy(x => x.TrackingNumber).ToListAsync(cancellationToken);
    }

    public async Task<int> RefreshAsync(string baseAddress, string branch, CancellationToken cancellationToken = default, bool useDelayTestEndpoint = false)
    {
        if (!ShipmentRules.ValidBranch(branch)) throw new ArgumentException("Enter a branch such as BR-001.");
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var root) ||
            (root.Scheme != Uri.UriSchemeHttp && root.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Enter an absolute HTTP or HTTPS service address.");
        var path = useDelayTestEndpoint ? "/api/test/shipments?branchCode=" : "/api/shipments?branchCode=";
        var uri = new Uri(root, path + Uri.EscapeDataString(branch) + (useDelayTestEndpoint ? "&mode=delay" : ""));
        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();   // non-2xx → HttpRequestException, so nothing is parsed or saved
        ApiResponse? payload;
        try { payload = await response.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken); }
        catch (JsonException ex) { throw new InvalidDataException("The service returned invalid JSON.", ex); }
        if (payload?.BranchCode != branch || payload.Shipments is null)
            throw new InvalidDataException("The response does not match the requested branch.");
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in payload.Shipments)
        {
            if (s is null || string.IsNullOrWhiteSpace(s.TrackingNumber) || s.BranchCode != branch ||
                string.IsNullOrWhiteSpace(s.Origin) || string.IsNullOrWhiteSpace(s.Destination) ||
                !ShipmentRules.Statuses.Contains(s.Status, StringComparer.Ordinal) ||
                s.LastUpdatedUtc is null || s.LastUpdatedUtc.Value.Offset != TimeSpan.Zero || !unique.Add(s.TrackingNumber))
                throw new InvalidDataException("The service returned invalid or duplicate shipment data.");
        }
        await using var db = new ShipmentDb(database);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        // Load this branch's existing rows once, keyed by tracking number.
        var existing = await db.Shipments
            .Where(s => s.BranchCode == branch)
            .ToDictionaryAsync(s => s.TrackingNumber, StringComparer.Ordinal, cancellationToken);

        foreach (var item in payload.Shipments)
        {
            if (!existing.TryGetValue(item.TrackingNumber!, out var saved))
            {
                saved = new Shipment { BranchCode = branch, TrackingNumber = item.TrackingNumber! };
                db.Shipments.Add(saved);                       // new shipment -> insert
            }
            saved.Origin = item.Origin!; saved.Destination = item.Destination!;   // existing -> update in place
            saved.Status = item.Status!; saved.LastUpdatedUtc = item.LastUpdatedUtc!.Value;
        }
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return payload.Shipments.Length;
    }
}
