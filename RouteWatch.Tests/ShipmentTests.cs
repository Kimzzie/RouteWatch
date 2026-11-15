using Microsoft.EntityFrameworkCore;
using RouteWatch.Core;
using RouteWatch.Desktop;
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
    [Fact]
    public async Task TC05_BranchQueriesReturnOnlyTheSelectedBranch()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");
        await env.RefreshWithAsync("branch-002.json", "BR-002");   // also contains TRACK-01

        Assert.Equal(3, (await env.SnapshotAsync()).Count);        // same tracking number, no collision

        var br1 = await env.Service.GetBranchAsync("BR-001");
        var br2 = await env.Service.GetBranchAsync("BR-002");

        Assert.Equal(new[] { "TRACK-01", "TRACK-02" }, br1.Select(s => s.TrackingNumber));
        Assert.All(br1, s => Assert.Equal("BR-001", s.BranchCode));
        var br1Track01 = br1.Single(s => s.TrackingNumber == "TRACK-01");
        Assert.Equal("Cagayan de Oro", br1Track01.Origin);
        Assert.Equal("Pending", br1Track01.Status);

        var only = Assert.Single(br2);
        Assert.Equal(("BR-002", "TRACK-01", "Davao", "Delivered"),
                     (only.BranchCode, only.TrackingNumber, only.Origin, only.Status));

        // Summaries and filters use only that branch's records.
        Assert.Equal(new ShipmentSummary(2, 1, 1, 0, 0), ShipmentReports.Summarize(br1));
        Assert.Equal(new ShipmentSummary(1, 0, 0, 1, 0), ShipmentReports.Summarize(br2));
        Assert.Equal(new[] { "TRACK-02" },
                     ShipmentReports.Filter(br1, "InTransit").Select(s => s.TrackingNumber));
        Assert.Empty(ShipmentReports.Filter(br2, "InTransit"));
    }

    [Fact]
    public async Task TC06_FilterChangesDisplayedRowsButNotTheBranchSummary()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");
        var vm = new MainViewModel(env.Service) { BranchCode = "BR-001" };
        await vm.LoadCommand.ExecuteAsync(null);

        AssertSummary(vm, total: 2, pending: 1, inTransit: 1, delivered: 0, exception: 0);
        Assert.Equal(new[] { "TRACK-01", "TRACK-02" }, vm.VisibleShipments.Select(s => s.TrackingNumber));

        vm.SelectedStatus = "InTransit";
        Assert.Equal(new[] { "TRACK-02" }, vm.VisibleShipments.Select(s => s.TrackingNumber));
        Assert.Equal(1, vm.VisibleCount);
        AssertSummary(vm, total: 2, pending: 1, inTransit: 1, delivered: 0, exception: 0); // unchanged

        vm.SelectedStatus = "All";
        Assert.Equal(2, vm.VisibleShipments.Count);
        AssertSummary(vm, total: 2, pending: 1, inTransit: 1, delivered: 0, exception: 0);
    }

    [Fact]
    public async Task TC06_SummaryCountsEveryStatus_ForADifferentCollection()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");
        await env.RefreshWithAsync("branch-001-update.json", "BR-001");  // now Delivered, InTransit, Exception
        var vm = new MainViewModel(env.Service) { BranchCode = "BR-001" };
        await vm.LoadCommand.ExecuteAsync(null);

        AssertSummary(vm, total: 3, pending: 0, inTransit: 1, delivered: 1, exception: 1);
        vm.SelectedStatus = "Exception";
        Assert.Equal(new[] { "TRACK-03" }, vm.VisibleShipments.Select(s => s.TrackingNumber));
        AssertSummary(vm, total: 3, pending: 0, inTransit: 1, delivered: 1, exception: 1);
    }

    private static void AssertSummary(MainViewModel vm, int total, int pending, int inTransit, int delivered, int exception) =>
        Assert.Equal((total, pending, inTransit, delivered, exception),
            (vm.TotalCount, vm.PendingCount, vm.InTransitCount, vm.DeliveredCount, vm.ExceptionCount));

    [Fact]
    public async Task TC07_ExportWritesExactlyTheDisplayedRows_WithCorrectEscaping()
    {
        await using var env = new TestEnvironment();
        await env.RefreshWithAsync("branch-001.json", "BR-001");   // TRACK-02 has a comma and quotes
        var vm = new MainViewModel(env.Service) { BranchCode = "BR-001" };
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedStatus = "InTransit";                           // displayed subset = TRACK-02 only

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            vm.ChooseExportFileAsync = _ => Task.FromResult<string?>(path);
            await vm.ExportCommand.ExecuteAsync(null);

            var rows = TestHelpers.ParseCsv(await File.ReadAllTextAsync(path));
            Assert.Equal(2, rows.Count);                           // header + exactly one row
            Assert.Equal(new[] { "TrackingNumber", "Origin", "Destination", "Status", "LastUpdatedUtc" }, rows[0]);
            Assert.Equal(new[] { "TRACK-02", "Depot, North", "Dock \"A\"", "InTransit" }, rows[1].Take(4));
            Assert.Equal(TestHelpers.Utc(2026, 11, 10, 9, 15),
                DateTimeOffset.Parse(rows[1][4], System.Globalization.CultureInfo.InvariantCulture));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TC07_CommasQuotesAndNewlinesInAField_RoundTripThroughCsv()
    {
        var s = new Shipment
        {
            TrackingNumber = "TRACK-X",
            BranchCode = "BR-001",
            Origin = "Line one\nLine two",
            Destination = "He said \"hi\", then left",
            Status = "Pending",
            LastUpdatedUtc = TestHelpers.Utc(2026, 11, 10, 8, 30)
        };

        var rows = TestHelpers.ParseCsv(ShipmentReports.Csv(new[] { s }));

        Assert.Equal(2, rows.Count);
        Assert.Equal(5, rows[1].Count);                            // the newline/comma did not split fields
        Assert.Equal(new[] { "TRACK-X", "Line one\nLine two", "He said \"hi\", then left", "Pending" },
                     rows[1].Take(4));
    }

    [Fact]
    public async Task TC08_SavedRecordsSurviveDisposingAndReopeningTheDatabase()
    {
        var dir = Path.Combine(Path.GetTempPath(), "routewatch-tc08-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var connection = $"Data Source={Path.Combine(dir, "shipments.db")};Pooling=False";
        try
        {
            // Session 1: save a valid response, then dispose everything.
            {
                var handler = new FakeHttpHandler { Body = TestEnvironment.Fixture("branch-001.json") };
                using var client = new HttpClient(handler);
                var options1 = new DbContextOptionsBuilder<ShipmentDb>().UseSqlite(connection).Options;
                var service1 = new ShipmentService(client, options1);
                await service1.InitializeAsync();
                Assert.Equal(2, await service1.RefreshAsync(TestHelpers.BaseUrl, "BR-001"));
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // Session 2: brand-new options, service and HttpClient; no network at all.
            var options2 = new DbContextOptionsBuilder<ShipmentDb>().UseSqlite(connection).Options;
            var service2 = new ShipmentService(new HttpClient(new FakeHttpHandler()), options2);
            await service2.InitializeAsync();             // must not wipe existing data
            var rows = await service2.GetBranchAsync("BR-001");

            Assert.Collection(rows,
                r => {
                    Assert.Equal("TRACK-01", r.TrackingNumber); Assert.Equal("Cagayan de Oro", r.Origin);
                    Assert.Equal("Valencia", r.Destination); Assert.Equal("Pending", r.Status);
                    Assert.Equal(TestHelpers.Utc(2026, 11, 10, 8, 30), r.LastUpdatedUtc);
                },
                r => {
                    Assert.Equal("TRACK-02", r.TrackingNumber); Assert.Equal("Depot, North", r.Origin);
                    Assert.Equal("Dock \"A\"", r.Destination); Assert.Equal("InTransit", r.Status);
                    Assert.Equal(TestHelpers.Utc(2026, 11, 10, 9, 15), r.LastUpdatedUtc);
                });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Ui_StartupConfigurationWarning_SurvivesTheInitialLoad()
    {
        await using var env = new TestEnvironment();
        var vm = new MainViewModel(env.Service)
        { BranchCode = "BR-001", StartupNotice = "Configuration could not be read." };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("Configuration could not be read.", vm.Message);
    }


}
