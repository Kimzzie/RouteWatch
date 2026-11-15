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
    [Fact(Skip = "Implement TC-03")] public void TC03() { }
    [Fact(Skip = "Implement TC-04")] public void TC04() { }
    [Fact(Skip = "Implement TC-05")] public void TC05() { }
    [Fact(Skip = "Implement TC-06")] public void TC06() { }
    [Fact(Skip = "Implement TC-07")] public void TC07() { }
    [Fact(Skip = "Implement TC-08")] public void TC08() { }
}
