using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;
using SaEkg.Infrastructure.Services;
using Xunit;

namespace SaEkg.Tests.Unit.Ecg;

public class PurgeServiceTests
{
    [Fact]
    public async Task PurgeRaw_marks_due_records_and_calls_store()
    {
        await using var db = CreateDb();
        var store = new FakeSignalStore();
        var patientId = Guid.NewGuid();
        var dueId = Guid.NewGuid();
        var laterId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = patientId,
            Email = "p@example.com",
            Role = UserRole.Patient,
            PasswordHash = "x",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.Patients.Add(new Patient { UserId = patientId, ConsentSigned = true });
        db.Devices.Add(new Device { Id = deviceId, Name = "D", SerialNumber = "SN1" });
        db.EcgRecords.AddRange(
            new EcgRecord
            {
                Id = dueId,
                PatientId = patientId,
                DeviceId = deviceId,
                UploadedAt = DateTimeOffset.UtcNow.AddDays(-40),
                OriginalFilename = "a.csv",
                SampleCount = 10,
                StartedAt = DateTime.UtcNow.AddDays(-40),
                EndedAt = DateTime.UtcNow.AddDays(-40),
                Status = EcgRecordStatus.Done,
                RawPurgeAt = DateTimeOffset.UtcNow.AddDays(-1),
                RawPurged = false
            },
            new EcgRecord
            {
                Id = laterId,
                PatientId = patientId,
                DeviceId = deviceId,
                UploadedAt = DateTimeOffset.UtcNow,
                OriginalFilename = "b.csv",
                SampleCount = 10,
                StartedAt = DateTime.UtcNow,
                EndedAt = DateTime.UtcNow,
                Status = EcgRecordStatus.Done,
                RawPurgeAt = DateTimeOffset.UtcNow.AddDays(20),
                RawPurged = false
            });
        await db.SaveChangesAsync();

        var svc = new PurgeService(db, store, NullLogger<PurgeService>.Instance);
        var count = await svc.PurgeRawAsync(DateTimeOffset.UtcNow);

        count.Should().Be(1);
        store.RawDeleted.Should().ContainSingle().Which.Should().Be(dueId);
        (await db.EcgRecords.SingleAsync(x => x.Id == dueId)).RawPurged.Should().BeTrue();
        (await db.EcgRecords.SingleAsync(x => x.Id == laterId)).RawPurged.Should().BeFalse();
    }

    [Fact]
    public async Task PurgeFiltered_marks_due_records_and_calls_store()
    {
        await using var db = CreateDb();
        var store = new FakeSignalStore();
        var patientId = Guid.NewGuid();
        var dueId = Guid.NewGuid();
        var laterId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        db.Users.Add(new User
        {
            Id = patientId,
            Email = "p2@example.com",
            Role = UserRole.Patient,
            PasswordHash = "x",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.Patients.Add(new Patient { UserId = patientId, ConsentSigned = true });
        db.Devices.Add(new Device { Id = deviceId, Name = "D2", SerialNumber = "SN2" });
        db.EcgRecords.AddRange(
            new EcgRecord
            {
                Id = dueId,
                PatientId = patientId,
                DeviceId = deviceId,
                UploadedAt = DateTimeOffset.UtcNow.AddYears(-11),
                OriginalFilename = "a.csv",
                SampleCount = 10,
                StartedAt = DateTime.UtcNow.AddYears(-11),
                EndedAt = DateTime.UtcNow.AddYears(-11),
                Status = EcgRecordStatus.Done,
                RawPurgeAt = DateTimeOffset.UtcNow.AddYears(-10),
                RawPurged = true,
                FilteredPurgeAt = DateTimeOffset.UtcNow.AddDays(-1),
                FilteredPurged = false
            },
            new EcgRecord
            {
                Id = laterId,
                PatientId = patientId,
                DeviceId = deviceId,
                UploadedAt = DateTimeOffset.UtcNow,
                OriginalFilename = "b.csv",
                SampleCount = 10,
                StartedAt = DateTime.UtcNow,
                EndedAt = DateTime.UtcNow,
                Status = EcgRecordStatus.Done,
                RawPurgeAt = DateTimeOffset.UtcNow.AddDays(20),
                FilteredPurgeAt = DateTimeOffset.UtcNow.AddYears(9),
                FilteredPurged = false
            });
        await db.SaveChangesAsync();

        var svc = new PurgeService(db, store, NullLogger<PurgeService>.Instance);
        var count = await svc.PurgeFilteredAsync(DateTimeOffset.UtcNow);

        count.Should().Be(1);
        store.FilteredDeleted.Should().ContainSingle().Which.Should().Be(dueId);
        (await db.EcgRecords.SingleAsync(x => x.Id == dueId)).FilteredPurged.Should().BeTrue();
        (await db.EcgRecords.SingleAsync(x => x.Id == laterId)).FilteredPurged.Should().BeFalse();
    }

    private static SaEkgDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SaEkgDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new InMemorySaEkgDbContext(options);
    }

    private sealed class InMemorySaEkgDbContext(DbContextOptions<SaEkgDbContext> options) : SaEkgDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasAnnotation("Npgsql:PostgresExtension:citext", null);
        }
    }

    private sealed class FakeSignalStore : IEcgSignalStore
    {
        public List<Guid> RawDeleted { get; } = [];
        public List<Guid> FilteredDeleted { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendRawSamplesAsync(Guid recordId, IReadOnlyList<RawSamplePoint> samples, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendFilteredSamplesAsync(Guid recordId, IReadOnlyList<FilteredSamplePoint> samples, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RawSamplePoint>> ReadRawSamplesAsync(Guid recordId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RawSamplePoint>>([]);
        public Task AppendFiducialsAsync(Guid recordId, IReadOnlyList<FiducialSamplePoint> fiducials, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DownsampledPoint>> ReadFilteredDownsampledAsync(Guid recordId, long fromMs, long toMs, int maxPoints, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DownsampledPoint>>([]);
        public Task DeleteByRecordIdAsync(Guid recordId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteRawSamplesAsync(Guid recordId, CancellationToken cancellationToken = default)
        {
            RawDeleted.Add(recordId);
            return Task.CompletedTask;
        }
        public Task DeleteFilteredAndFiducialsAsync(Guid recordId, CancellationToken cancellationToken = default)
        {
            FilteredDeleted.Add(recordId);
            return Task.CompletedTask;
        }
    }
}
