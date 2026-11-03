using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SaEkg.Application.Auth;
using SaEkg.Application.Csv;
using SaEkg.Application.Devices;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class EcgUploadTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("saekg")
                .WithUsername("saekg")
                .WithPassword("saekg")
                .Build();
            await _postgres.StartAsync();
        }
        catch (DockerUnavailableException)
        {
            _postgres = null;
            return;
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("DuckDb:Path", Path.Combine(Path.GetTempPath(), $"saekg-{Guid.NewGuid():N}.duckdb"));
            builder.UseSetting("Jwt:SigningKey", "test-signing-key-which-is-long-enough-123456");
            builder.UseSetting("Security:EnforceInternalNetworkCheck", "false");
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Upload_requires_consent_then_accepts_valid_csv()
    {
        Skip.If(_factory is null, "Docker is not available");

        var client = _factory!.CreateClient();
        var email = $"p_{Guid.NewGuid():N}@example.com";
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Password1"));
        reg.EnsureSuccessStatusCode();
        var tokens = await reg.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var deviceId = await CreateDeviceAsAdminAsync();

        using var csvNoConsent = EcgCsvGenerator.Generate(TimeSpan.FromSeconds(10));
        var denied = await PostUploadAsync(client, deviceId, csvNoConsent);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await GrantConsentAsync(tokens.UserId);

        using var csv = EcgCsvGenerator.Generate(TimeSpan.FromSeconds(10));
        var accepted = await PostUploadAsync(client, deviceId, csv);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await accepted.Content.ReadFromJsonAsync<EcgUploadAcceptedResponse>();
        body.Should().NotBeNull();

        var get = await client.GetAsync($"/api/v1/ecg-records/{body!.RecordId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var record = await get.Content.ReadFromJsonAsync<EcgRecordDto>();
        record!.Status.Should().Be(EcgRecordStatus.Queued);
        record.SampleCount.Should().Be(2500);
    }

    [SkippableFact]
    public async Task Upload_invalid_value_returns_csv_check_failed_with_line()
    {
        Skip.If(_factory is null, "Docker is not available");

        var client = _factory!.CreateClient();
        var email = $"p_{Guid.NewGuid():N}@example.com";
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Password1"));
        var tokens = await reg.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        await GrantConsentAsync(tokens.UserId);
        var deviceId = await CreateDeviceAsAdminAsync();

        using var bad = BuildCsv(2500, i => i == 5 ? "5001" : "100");
        var response = await PostUploadAsync(client, deviceId, bad);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        doc.RootElement.GetProperty("code").GetString().Should().Be("CSV_CHECK_FAILED");
        doc.RootElement.GetProperty("details").GetProperty("check").GetInt32().Should().Be(5);
        doc.RootElement.GetProperty("details").GetProperty("line").GetInt32().Should().Be(7);
    }

    private async Task<Guid> CreateDeviceAsAdminAsync()
    {
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        const string password = "AdminPass1";
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher<User>>();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Role = UserRole.Admin,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var admin = _factory.CreateClient();
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var create = await admin.PostAsJsonAsync("/api/v1/admin/devices", new CreateDeviceRequest("H1", $"SN-{Guid.NewGuid():N}"[..12], null, null));
        create.EnsureSuccessStatusCode();
        var device = await create.Content.ReadFromJsonAsync<DeviceDto>();
        return device!.Id;
    }

    private async Task GrantConsentAsync(Guid patientId)
    {
        var email = $"admin_c_{Guid.NewGuid():N}@example.com";
        const string password = "AdminPass1";
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher<User>>();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                Role = UserRole.Admin,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var admin = _factory.CreateClient();
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var consent = await admin.PostAsync($"/api/v1/admin/patients/{patientId}/consent", null);
        consent.EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> PostUploadAsync(HttpClient client, Guid deviceId, Stream csv)
    {
        csv.Position = 0;
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(deviceId.ToString()), "deviceId");
        var fileContent = new StreamContent(csv);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "ecg.csv");
        return await client.PostAsync("/api/v1/ecg-records", form);
    }

    private static MemoryStream BuildCsv(int sampleCount, Func<int, string> valueFactory)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("timestamp,value,errors\n");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, 0);
        for (var i = 0; i < sampleCount; i++)
        {
            var ts = start.AddMilliseconds(i * 4);
            sb.Append($"{ts:yyyy-MM-dd HH:mm:ss.fff},{valueFactory(i)},NoError\n");
        }

        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
    }
}

