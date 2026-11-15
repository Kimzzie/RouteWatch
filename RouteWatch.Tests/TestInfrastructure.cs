using System.Net;
using Microsoft.EntityFrameworkCore;
using RouteWatch.Core;

namespace RouteWatch.Tests;

// This infrastructure is supplied for students to build their own assertions.
public sealed class FakeHttpHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string Body { get; set; } = "";
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public Uri? LastRequest { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request.RequestUri;
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
        return new HttpResponseMessage(Status) { Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json") };
    }
}

public sealed class TestEnvironment : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "routewatch-tests-" + Guid.NewGuid().ToString("N"));
    public FakeHttpHandler Handler { get; } = new();
    public HttpClient Client { get; }
    public DbContextOptions<ShipmentDb> Options { get; }
    public ShipmentService Service { get; }
    public TestEnvironment()
    {
        Directory.CreateDirectory(_directory);
        Options = new DbContextOptionsBuilder<ShipmentDb>().UseSqlite($"Data Source={Path.Combine(_directory, "shipments.db")};Pooling=False").Options;
        Client = new HttpClient(Handler);
        Service = new ShipmentService(Client, Options);
    }
    public static string Fixture(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Yield();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
