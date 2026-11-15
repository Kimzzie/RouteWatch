using System.Net;
using Microsoft.EntityFrameworkCore;
using RouteWatch.Core;

namespace RouteWatch.Tests;

// A plain copy of what is stored, so before/after states can be compared with Assert.Equal.
public sealed record StoredRow(
    string BranchCode, string TrackingNumber, string Origin,
    string Destination, string Status, DateTimeOffset LastUpdatedUtc);

public static class TestHelpers
{
    public const string BaseUrl = "http://hub.test";

    public static DateTimeOffset Utc(int y, int m, int d, int h, int min) =>
        new(y, m, d, h, min, 0, TimeSpan.Zero);

    // Feed a fixture file to the fake HTTP handler, then run a real refresh.
    public static async Task<int> RefreshWithAsync(
        this TestEnvironment env, string fixture, string branch,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        await env.Service.InitializeAsync();
        env.Handler.Status = status;
        env.Handler.Body = TestEnvironment.Fixture(fixture);
        return await env.Service.RefreshAsync(BaseUrl, branch);
    }

    // Read EVERY row straight from the database (all branches), in a stable order.
    public static async Task<List<StoredRow>> SnapshotAsync(this TestEnvironment env)
    {
        await env.Service.InitializeAsync();
        await using var db = new ShipmentDb(env.Options);
        return await db.Shipments.AsNoTracking()
            .OrderBy(s => s.BranchCode).ThenBy(s => s.TrackingNumber)
            .Select(s => new StoredRow(s.BranchCode, s.TrackingNumber, s.Origin,
                                       s.Destination, s.Status, s.LastUpdatedUtc))
            .ToListAsync();
    }
}