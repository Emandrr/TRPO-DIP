using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SaEkg.Application.Auth;
using SaEkg.Application.Devices;
using SaEkg.Application.Logging;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class AdminDevicesAndLogsTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;
    private string? _adminToken;

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

        var email = $"admin_{Guid.NewGuid():N}@example.com";
        const string password = "AdminPass1";
        using (var scope = _factory.Services.CreateScope())
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

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        _adminToken = tokens!.AccessToken;
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
    public async Task Device_crud_requires_admin_and_soft_deletes()
    {
        Skip.If(_factory is null, "Docker is not available; start Docker Desktop to run admin API tests.");

        var admin = _factory!.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var patientReg = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest($"pat_{Guid.NewGuid():N}@example.com", "Password1"));
        patientReg.EnsureSuccessStatusCode();
        var patientTokens = await patientReg.Content.ReadFromJsonAsync<TokenResponse>();
        var patient = _factory.CreateClient();
        patient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", patientTokens!.AccessToken);

        var forbidden = await patient.GetAsync("/api/v1/admin/devices");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var create = await admin.PostAsJsonAsync("/api/v1/admin/devices", new CreateDeviceRequest(
            "Holter A", "SN-100", "M1", null));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<DeviceDto>();
        created.Should().NotBeNull();

        var list = await admin.GetFromJsonAsync<List<DeviceDto>>("/api/v1/admin/devices");
        list.Should().Contain(x => x.Id == created!.Id);

        var delete = await admin.DeleteAsync($"/api/v1/admin/devices/{created!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfter = await admin.GetFromJsonAsync<List<DeviceDto>>("/api/v1/admin/devices");
        listAfter.Should().NotContain(x => x.Id == created.Id);
    }

    [SkippableFact]
    public async Task Admin_logs_endpoint_filters_and_paginates()
    {
        Skip.If(_factory is null, "Docker is not available; start Docker Desktop to run admin API tests.");

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
            db.SystemLogs.Add(new SystemLog
            {
                Ts = DateTimeOffset.UtcNow,
                Level = "Warning",
                Module = "CsvValidation",
                Code = "CSV_CHECK_5",
                Message = "bad value"
            });
            await db.SaveChangesAsync();
        }

        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await admin.GetAsync("/api/v1/admin/logs?module=CsvValidation&code=CSV_CHECK_5&page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<SystemLogDto>>();
        page.Should().NotBeNull();
        page!.Total.Should().BeGreaterThan(0);
        page.Items.Should().Contain(x => x.Code == "CSV_CHECK_5" && x.Module == "CsvValidation");
    }
}
