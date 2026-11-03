using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SaEkg.Application.Auth;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class AuthFlowTests : IAsyncLifetime
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
    public async Task Register_login_refresh_reuse_revokes_chain()
    {
        Skip.If(_factory is null, "Docker is not available");

        var client = _factory!.CreateClient();
        var email = $"p_{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Password1"));
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await register.Content.ReadFromJsonAsync<TokenResponse>();
        tokens.Should().NotBeNull();

        var conflict = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Password1"));
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var refresh1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens!.RefreshToken));
        refresh1.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await refresh1.Content.ReadFromJsonAsync<TokenResponse>();
        rotated.Should().NotBeNull();

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var reuseNew = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(rotated!.RefreshToken));
        reuseNew.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Lockout_after_five_failed_logins()
    {
        Skip.If(_factory is null, "Docker is not available");

        var client = _factory!.CreateClient();
        var email = $"lock_{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Password1")))
            .EnsureSuccessStatusCode();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "WrongPass1"));
        }

        last!.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    [SkippableFact]
    public async Task Temp_password_forces_change_before_admin_devices()
    {
        Skip.If(_factory is null, "Docker is not available");

        var adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        const string adminPassword = "AdminPass1";
        await SeedAdminAsync(adminEmail, adminPassword);

        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(adminEmail, adminPassword));
        login.EnsureSuccessStatusCode();
        var adminTokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens!.AccessToken);

        var createStaff = await client.PostAsJsonAsync("/api/v1/admin/users", new CreateStaffUserRequest("doc@example.com", UserRole.Doctor));
        createStaff.StatusCode.Should().Be(HttpStatusCode.Created);
        var staff = await createStaff.Content.ReadFromJsonAsync<CreatedStaffUserResponse>();
        staff.Should().NotBeNull();

        var doctorClient = _factory.CreateClient();
        var doctorLogin = await doctorClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("doc@example.com", staff!.TemporaryPassword));
        doctorLogin.EnsureSuccessStatusCode();
        var doctorTokens = await doctorLogin.Content.ReadFromJsonAsync<TokenResponse>();
        doctorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doctorTokens!.AccessToken);

        var denied = await doctorClient.GetAsync("/api/v1/admin/devices");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var change = await doctorClient.PostAsJsonAsync("/api/v1/auth/change-password", new ChangePasswordRequest(staff.TemporaryPassword, "NewPass12"));
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stillForbiddenByRole = await doctorClient.GetAsync("/api/v1/admin/devices");
        stillForbiddenByRole.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var devicesOk = await client.GetAsync("/api/v1/admin/devices");
        devicesOk.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task SeedAdminAsync(string email, string password)
    {
        using var scope = _factory!.Services.CreateScope();
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
}
