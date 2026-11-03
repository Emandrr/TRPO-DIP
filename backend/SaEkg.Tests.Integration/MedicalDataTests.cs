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
using SaEkg.Application.Medical;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class MedicalDataTests : IAsyncLifetime
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
    public async Task Doctor_crud_medical_flow_patient_reads_admin_deletes()
    {
        Skip.If(_factory is null, "Docker is not available");

        var (patientId, patientClient) = await RegisterPatientAsync();
        var (doctorId, doctorClient) = await CreateDoctorAsync();
        var adminClient = await CreateAdminClientAsync();

        await AssignAsync(adminClient, doctorId, patientId);

        var cardBody = new UpsertMedicalCardRequest("Иванов", "Иван", "Иванович", "15.03.1990", "мужской");
        var upsert = await doctorClient.PutAsJsonAsync($"/api/v1/medical-cards/{patientId}", cardBody);
        upsert.StatusCode.Should().Be(HttpStatusCode.OK);
        var card = await upsert.Content.ReadFromJsonAsync<MedicalCardDto>();
        card!.Sex.Should().Be("мужской");
        card.BirthDate.Should().Be("15.03.1990");

        var patientCard = await patientClient.GetAsync($"/api/v1/medical-cards/{patientId}");
        patientCard.StatusCode.Should().Be(HttpStatusCode.OK);

        var badCard = await doctorClient.PutAsJsonAsync(
            $"/api/v1/medical-cards/{patientId}",
            new UpsertMedicalCardRequest("", "Иван", null, "15.03.1990", "мужской"));
        badCard.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var diagnosisCreate = await doctorClient.PostAsJsonAsync("/api/v1/diagnoses", new CreateDiagnosisRequest(
            patientId,
            "Синусовая тахикардия",
            "Комментарий",
            DateOnly.FromDateTime(DateTime.UtcNow.Date).ToString("dd.MM.yyyy"),
            null,
            null,
            null));
        diagnosisCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var diagnosis = await diagnosisCreate.Content.ReadFromJsonAsync<DiagnosisDto>();
        diagnosis.Should().NotBeNull();

        var rec = await doctorClient.PostAsJsonAsync(
            $"/api/v1/diagnoses/{diagnosis!.Id}/recommendations",
            new UpsertRecommendationRequest("Контроль ЧСС", null));
        rec.StatusCode.Should().Be(HttpStatusCode.Created);

        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)).ToString("dd.MM.yyyy");
        var end = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14)).ToString("dd.MM.yyyy");
        var rx = await doctorClient.PostAsJsonAsync(
            $"/api/v1/diagnoses/{diagnosis.Id}/prescriptions",
            new UpsertPrescriptionRequest("Метопролол", start, end, null));
        rx.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await patientClient.GetAsync($"/api/v1/patients/{patientId}/diagnoses");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var diagnoses = await list.Content.ReadFromJsonAsync<List<DiagnosisDto>>();
        diagnoses.Should().ContainSingle(d => d.Id == diagnosis.Id);

        var delDiag = await adminClient.DeleteAsync($"/api/v1/diagnoses/{diagnosis.Id}");
        delDiag.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var gone = await patientClient.GetAsync($"/api/v1/diagnoses/{diagnosis.Id}");
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var delCard = await adminClient.DeleteAsync($"/api/v1/medical-cards/{patientId}");
        delCard.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cardGone = await patientClient.GetAsync($"/api/v1/medical-cards/{patientId}");
        cardGone.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
        db.AuditAccesses.Count(a => a.Entity == "medical_card" || a.Entity == "diagnosis").Should().BeGreaterThan(0);
    }

    [SkippableTheory]
    [InlineData(UserRole.Patient, HttpStatusCode.NotFound)]
    [InlineData(UserRole.Doctor, HttpStatusCode.Forbidden)]
    public async Task Foreign_patient_card_access_denied(string actorRole, HttpStatusCode expected)
    {
        Skip.If(_factory is null, "Docker is not available");

        var (patientId, _) = await RegisterPatientAsync();
        var (otherPatientId, otherPatientClient) = await RegisterPatientAsync();
        var (doctorId, doctorClient) = await CreateDoctorAsync();
        var adminClient = await CreateAdminClientAsync();
        await AssignAsync(adminClient, doctorId, patientId);

        await doctorClient.PutAsJsonAsync(
            $"/api/v1/medical-cards/{patientId}",
            new UpsertMedicalCardRequest("Петров", "Пётр", null, "01.01.1980", "мужской"));

        HttpClient actor = actorRole == UserRole.Patient ? otherPatientClient : doctorClient;
        Guid target = actorRole == UserRole.Patient ? patientId : otherPatientId;

        if (actorRole == UserRole.Doctor)
        {
            var createDenied = await doctorClient.PutAsJsonAsync(
                $"/api/v1/medical-cards/{otherPatientId}",
                new UpsertMedicalCardRequest("Сидоров", "Сидор", null, "01.01.1985", "мужской"));
            createDenied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var get = await actor.GetAsync($"/api/v1/medical-cards/{target}");
        get.StatusCode.Should().Be(expected);
    }

    [SkippableFact]
    public async Task Patient_cannot_mutate_medical_data()
    {
        Skip.If(_factory is null, "Docker is not available");

        var (patientId, patientClient) = await RegisterPatientAsync();
        var put = await patientClient.PutAsJsonAsync(
            $"/api/v1/medical-cards/{patientId}",
            new UpsertMedicalCardRequest("Иванов", "Иван", null, "15.03.1990", "мужской"));
        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var create = await patientClient.PostAsJsonAsync("/api/v1/diagnoses", new CreateDiagnosisRequest(
            patientId, "X", null, "01.01.2020", null, null, null));
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        created.Should().NotBeNull();

        var client = _factory!.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, created!.TemporaryPassword));
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        if (tokens.MustChangePassword)
        {
            var change = await client.PostAsJsonAsync(
                "/api/v1/auth/change-password",
                new ChangePasswordRequest(created.TemporaryPassword, "DoctorPass1"));
            change.EnsureSuccessStatusCode();
            login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "DoctorPass1"));
            login.EnsureSuccessStatusCode();
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
        var response = await admin.PutAsJsonAsync("/api/v1/admin/assignments", new AssignmentRequest(doctorId, patientId));
        response.EnsureSuccessStatusCode();
    }
}
