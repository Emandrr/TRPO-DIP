using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SaEkg.Tests.Integration;

public class HealthAndErrorFormatTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthAndErrorFormatTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ => { });
    }

    [Fact]
    public async Task Health_returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unhandled_exception_returns_unified_error_json()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/v1/_debug/throw");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.Contains("X-Request-Id").Should().BeTrue();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;
        root.GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
        root.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Incoming_request_id_is_echoed()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Request-Id", "test-request-id-001");
        var response = await client.SendAsync(request);
        response.Headers.TryGetValues("X-Request-Id", out var values).Should().BeTrue();
        values!.Should().ContainSingle("test-request-id-001");
    }
}
