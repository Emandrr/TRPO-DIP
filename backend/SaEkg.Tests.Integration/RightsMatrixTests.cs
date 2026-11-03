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
using SaEkg.Application.Medical;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class RightsMatrixTests : IAsyncLifetime
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

    [SkippableTheory]
    [InlineData("Patient", "own", HttpStatusCode.OK)]
    [InlineData("Patient", "foreign", HttpStatusCode.NotFound)]
    [InlineData("Doctor", "assigned", HttpStatusCode.OK)]
    [InlineData("Doctor", "unassigned", HttpStatusCode.Forbidden)]
    [InlineData("Admin", "any", HttpStatusCode.OK)]
    public async Task Medical_card_read_matrix(string actor, string relation, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");
        var (patientA, patientAClient) = await RegisterPatientAsync();
        var (patientB, _) = await RegisterPatientAsync();
        var (doctorAssigned, doctorAssignedClient) = await CreateDoctorAsync();
        var (_, doctorForeignClient) = await CreateDoctorAsync();
        var admin = await CreateAdminClientAsync();
        await AssignAsync(admin, doctorAssigned, patientA);
        await UpsertCardAsync(doctorAssignedClient, patientA);

        var target = relation is "own" or "assigned" ? patientA : patientB;
        if (actor == "Admin")
        {
            target = patientA;
        }

        var client = actor switch
        {
            "Patient" => patientAClient,
            "Doctor" => relation == "assigned" ? doctorAssignedClient : doctorForeignClient,
            _ => admin
        };

        var response = await client.GetAsync($"/api/v1/medical-cards/{target}");
        response.StatusCode.Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData("Patient", HttpStatusCode.Forbidden)]
    [InlineData("Doctor", HttpStatusCode.OK)]
    [InlineData("Admin", HttpStatusCode.Forbidden)]
    public async Task Medical_card_upsert_matrix(string actor, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");
        var (patientId, patientClient) = await RegisterPatientAsync();
        var (doctorId, doctorClient) = await CreateDoctorAsync();
        var admin = await CreateAdminClientAsync();
        await AssignAsync(admin, doctorId, patientId);

        var client = actor switch
        {
            "Patient" => patientClient,
            "Doctor" => doctorClient,
            _ => admin
        };

        var response = await client.PutAsJsonAsync(
            $"/api/v1/medical-cards/{patientId}",
            new UpsertMedicalCardRequest("Иванов", "Иван", null, "15.03.1990", "мужской"));
        response.StatusCode.Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData("Patient", HttpStatusCode.Forbidden)]
    [InlineData("Doctor", HttpStatusCode.NoContent)]
    [InlineData("Admin", HttpStatusCode.NoContent)]
    public async Task Medical_card_delete_matrix(string actor, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");
        var (patientId, patientClient) = await RegisterPatientAsync();
        var (doctorId, doctorClient) = await CreateDoctorAsync();
        var admin = await CreateAdminClientAsync();
        await AssignAsync(admin, doctorId, patientId);
        await UpsertCardAsync(doctorClient, patientId);

        var client = actor switch
        {
            "Patient" => patientClient,
            "Doctor" => doctorClient,
            _ => admin
        };

        var response = await client.DeleteAsync($"/api/v1/medical-cards/{patientId}");
        response.StatusCode.Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData("Patient", HttpStatusCode.Forbidden)]
    [InlineData("Doctor", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Admin_users_list_matrix(string actor, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");
        var (_, patientClient) = await RegisterPatientAsync();
        var (_, doctorClient) = await CreateDoctorAsync();
        var admin = await CreateAdminClientAsync();
        var client = actor switch
        {
            "Patient" => patientClient,
            "Doctor" => doctorClient,
            _ => admin
        };

        (await client.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData("Patient", HttpStatusCode.Forbidden)]
    [InlineData("Doctor", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Admin_devices_and_logs_matrix(string actor, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");
        var (_, patientClient) = await RegisterPatientAsync();
        var (_, doctorClient) = await CreateDoctorAsync();
        var admin = await CreateAdminClientAsync();
        var client = actor switch
        {
            "Patient" => patientClient,
            "Doctor" => doctorClient,
            _ => admin
        };

        (await client.GetAsync("/api/v1/admin/devices")).StatusCode.Should().Be(expected);
        (await client.GetAsync("/api/v1/admin/logs")).StatusCode.Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData("Patient", HttpStatusCode.OK)]
    [InlineData("Doctor", HttpStatusCode.OK)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Devices_list_readable_by_authenticated(string actor, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");
        var admin = await CreateAdminClientAsync();
        await admin.PostAsJsonAsync("/api/v1/admin/devices", new CreateDeviceRequest("Dev", "SN-" + Guid.NewGuid().ToString("N")[..8], null, null));
        var (_, patientClient) = await RegisterPatientAsync();
        var (_, doctorClient) = await CreateDoctorAsync();
        var client = actor switch
        {
            "Patient" => patientClient,
            "Doctor" => doctorClient,
            _ => admin
        };

        (await client.GetAsync("/api/v1/devices")).StatusCode.Should().Be(expected);
    }

    [SkippableTheory]
    [InlineData("Patient", HttpStatusCode.Forbidden)]
    [InlineData("Doctor", HttpStatusCode.Created)]
    public async Task Diagnosis_create_matrix(string actor, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");
        var (patientId, patientClient) = await RegisterPatientAsync();
        var (doctorId, doctorClient) = await CreateDoctorAsync();
        var admin = await CreateAdminClientAsync();
        await AssignAsync(admin, doctorId, patientId);

        var client = actor == "Patient" ? patientClient : doctorClient;
        var response = await client.PostAsJsonAsync("/api/v1/diagnoses", new CreateDiagnosisRequest(
            patientId,
            "Диагноз",
            null,
            DateOnly.FromDateTime(DateTime.UtcNow).ToString("dd.MM.yyyy"),
            null,
            null,
            null));
        response.StatusCode.Should().Be(expected);
    }

    [SkippableFact]
    public async Task Anonymous_protected_endpoints_return_401()
    {
        Skip.If(_factory is null, "Docker is not available");
        var client = _factory!.CreateClient();
        (await client.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/devices")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/ecg-records")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task UpsertCardAsync(HttpClient doctor, Guid patientId)
    {
        var response = await doctor.PutAsJsonAsync(
            $"/api/v1/medical-cards/{patientId}",
            new UpsertMedicalCardRequest("Иванов", "Иван", null, "15.03.1990", "мужской"));
        response.EnsureSuccessStatusCode();
    }

    private async Task<(Guid PatientId, HttpClient Client)> RegisterPatientAsync()
    {
        var client = _factory!.CreateClient();
        var email = $"p_{Guid.NewGuid():N}@example.com";
        var reg = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Password1"));
        reg.EnsureSuccessStatusCode();
        var tokens = await reg.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return (tokens.UserId, client);
    }

    private async Task<(Guid DoctorId, HttpClient Client)> CreateDoctorAsync()
    {
        var admin = await CreateAdminClientAsync();
        var email = $"doc_{Guid.NewGuid():N}@example.com";
        var create = await admin.PostAsJsonAsync("/api/v1/admin/users", new CreateStaffUserRequest(email, UserRole.Doctor));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<CreatedStaffUserResponse>();
        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, created!.TemporaryPassword));
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        if (tokens.MustChangePassword)
        {
            await client.PostAsJsonAsync("/api/v1/auth/change-password", new ChangePasswordRequest(created.TemporaryPassword, "DoctorPass1"));
            login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "DoctorPass1"));
            tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        }

        return (created.User.Id, client);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
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

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    private static async Task AssignAsync(HttpClient admin, Guid doctorId, Guid patientId)
    {
        (await admin.PutAsJsonAsync("/api/v1/admin/assignments", new AssignmentRequest(doctorId, patientId)))
            .EnsureSuccessStatusCode();
    }
}
