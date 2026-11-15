using System.Net;
using Xunit;

namespace RouteWatch.Tests;

public sealed class ShipmentTests
{
    // Implement independent tests using the supplied test infrastructure and published case specifications.
    [Fact]
    public async Task TC01_ValidResponse_StoresBothShipmentsForRequestedBranch()
    {
        await using var env = new TestEnvironment();               // fresh temp DB per test
        Assert.Empty(await env.SnapshotAsync());                   // precondition: empty

        var received = await env.RefreshWithAsync("branch-001.json", "BR-001");

        Assert.Equal(2, received);
        Assert.Equal("/api/shipments", env.Handler.LastRequest!.AbsolutePath);
        Assert.Equal("?branchCode=BR-001", env.Handler.LastRequest.Query); // requested the right branch

        var rows = await env.SnapshotAsync();
        Assert.Collection(rows,
            r => Assert.Equal(new StoredRow("BR-001", "TRACK-01", "Cagayan de Oro", "Valencia",
                                            "Pending", TestHelpers.Utc(2026, 11, 10, 8, 30)), r),
            r => Assert.Equal(new StoredRow("BR-001", "TRACK-02", "Depot, North", "Dock \"A\"",
                                            "InTransit", TestHelpers.Utc(2026, 11, 10, 9, 15)), r));
        Assert.All(rows, r => Assert.Equal(TimeSpan.Zero, r.LastUpdatedUtc.Offset)); // still UTC
    }

    [Fact]
    public async Task TC01_ResponseForDifferentBranch_IsRejectedWithoutChangingData()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");
        var before = await env.SnapshotAsync();

        // We ask for BR-001 but the server answers with BR-002 data.
        await Assert.ThrowsAsync<InvalidDataException>(
            () => env.RefreshWithAsync("branch-002.json", "BR-001"));

        Assert.Equal(before, await env.SnapshotAsync());           // nothing added or changed
    }

    [Fact]
    public async Task TC02_HttpFailure_ReportsFailureAndKeepsSavedRecords()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");   // precondition: valid saved data
        var before = await env.SnapshotAsync();
        Assert.Equal(2, before.Count);

        // A failed response whose body still looks like a valid (empty) payload.
        env.Handler.Status = HttpStatusCode.InternalServerError;
        env.Handler.Body = TestEnvironment.Fixture("empty-001.json");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => env.Service.RefreshAsync(TestHelpers.BaseUrl, "BR-001"));

        Assert.Equal(before, await env.SnapshotAsync());           // identical, field by field
    }
    [Fact]
    public async Task TC03_InvalidJson_ReportsInvalidDataAndKeepsSavedRecords()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");   // precondition: valid saved data
        var before = await env.SnapshotAsync();
        Assert.Equal(2, before.Count);

        env.Handler.Status = HttpStatusCode.OK;                    // HTTP is fine, the body is broken
        env.Handler.Body = TestEnvironment.Fixture("invalid.json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => env.Service.RefreshAsync(TestHelpers.BaseUrl, "BR-001"));

        Assert.Equal(before, await env.SnapshotAsync());
    }

    [Fact]
    public async Task TC03_ValidJsonButMissingFields_IsRejectedAndKeepsSavedRecords()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");
        var before = await env.SnapshotAsync();

        env.Handler.Status = HttpStatusCode.OK;
        env.Handler.Body = """{"branchCode":"BR-001","shipments":[{"trackingNumber":"TRACK-09"}]}""";

        await Assert.ThrowsAsync<InvalidDataException>(
            () => env.Service.RefreshAsync(TestHelpers.BaseUrl, "BR-001"));

        Assert.Equal(before, await env.SnapshotAsync());
    }

    [Fact]
    public async Task TC04_RepeatedAndUpdatedRefresh_IsIdempotentAndNeverDeletes()
    {
        await using var env = new TestEnvironment();

        // 1) Same response twice -> no duplicates.
        await env.RefreshWithAsync("branch-001.json", "BR-001");
        await env.RefreshWithAsync("branch-001.json", "BR-001");
        var afterRepeat = await env.SnapshotAsync();
        Assert.Equal(2, afterRepeat.Count);
        AssertUniqueIdentities(afterRepeat);
        var track02Before = afterRepeat.Single(r => r.TrackingNumber == "TRACK-02");

        // 2) Update: TRACK-01 changes, TRACK-03 is new, TRACK-02 is omitted but must stay.
        await env.RefreshWithAsync("branch-001-update.json", "BR-001");
        var afterUpdate = await env.SnapshotAsync();
        Assert.Equal(3, afterUpdate.Count);
        AssertUniqueIdentities(afterUpdate);
        Assert.Equal(
            new StoredRow("BR-001", "TRACK-01", "Cagayan de Oro", "Valencia", "Delivered",
                          TestHelpers.Utc(2026, 11, 12, 10, 0)),
            afterUpdate.Single(r => r.TrackingNumber == "TRACK-01"));
        Assert.Equal(
            new StoredRow("BR-001", "TRACK-03", "Iligan", "Butuan", "Exception",
                          TestHelpers.Utc(2026, 11, 12, 11, 0)),
            afterUpdate.Single(r => r.TrackingNumber == "TRACK-03"));
        Assert.Equal(track02Before, afterUpdate.Single(r => r.TrackingNumber == "TRACK-02"));

        // 3) Empty response -> deletes nothing.
        await env.RefreshWithAsync("empty-001.json", "BR-001");
        Assert.Equal(afterUpdate, await env.SnapshotAsync());
    }

    private static void AssertUniqueIdentities(IEnumerable<StoredRow> rows) =>
        Assert.Equal(rows.Count(),
            rows.Select(r => (r.BranchCode, r.TrackingNumber)).Distinct().Count());
    [Fact(Skip = "Implement TC-05")] public void TC05() { }
    [Fact(Skip = "Implement TC-06")] public void TC06() { }
    [Fact(Skip = "Implement TC-07")] public void TC07() { }
    [Fact(Skip = "Implement TC-08")] public void TC08() { }
}
