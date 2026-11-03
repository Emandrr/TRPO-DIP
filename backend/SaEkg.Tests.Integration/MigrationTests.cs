using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class MigrationTests
{
    [SkippableFact]
    public async Task Migrations_apply_on_empty_database()
    {
        PostgreSqlContainer postgres;
        try
        {
            postgres = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("saekg")
                .WithUsername("saekg")
                .WithPassword("saekg")
                .Build();
        }
        catch (DockerUnavailableException)
        {
            throw new SkipException("Docker is not available; start Docker Desktop to run migration tests.");
        }

        await using (postgres)
        {
            try
            {
                await postgres.StartAsync();
            }
            catch (DockerUnavailableException)
            {
                throw new SkipException("Docker is not available; start Docker Desktop to run migration tests.");
            }

            var options = new DbContextOptionsBuilder<SaEkgDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options;

            await using var db = new SaEkgDbContext(options);
            await db.Database.MigrateAsync();

            var device = new Device
            {
                Id = Guid.NewGuid(),
                Name = "Holter-1",
                SerialNumber = "SN-001"
            };
            db.Devices.Add(device);
            await db.SaveChangesAsync();

            var loaded = await db.Devices.SingleAsync(x => x.Id == device.Id);
            loaded.SerialNumber.Should().Be("SN-001");

            device.IsDeleted = true;
            device.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            (await db.Devices.AnyAsync()).Should().BeFalse();
            (await db.Devices.IgnoreQueryFilters().AnyAsync(x => x.Id == device.Id)).Should().BeTrue();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "patient@example.com",
                PasswordHash = "hash",
                Role = UserRole.Patient,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            (await db.Users.CountAsync()).Should().Be(1);
        }
    }
}
