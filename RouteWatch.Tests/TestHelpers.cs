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
    // Minimal RFC 4180 parser: handles quoted fields, "" escapes, and newlines inside quotes.
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new(); }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}