using System.Net;
using Xunit;

namespace RouteWatch.Tests;

// A non-assessed example of Arrange, Act, Assert. This does not implement a PT6 test case.
public sealed class ExampleHttpHandlerTest
{
    [Fact]
    public async Task FakeHandlerReturnsTheConfiguredReply()
    {
        // Arrange: configure a fake endpoint and a client that uses it.
        using var handler = new FakeHttpHandler { Status = HttpStatusCode.Accepted, Body = "classroom example" };
        using var client = new HttpClient(handler);

        // Act: send a request to the fake handler; no network or live API is involved.
        using var response = await client.GetAsync("http://example.test/demo");
        var body = await response.Content.ReadAsStringAsync();

        // Assert: verify both the response and the address the handler observed.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("classroom example", body);
        Assert.Equal("/demo", handler.LastRequest!.AbsolutePath);
    }
}
