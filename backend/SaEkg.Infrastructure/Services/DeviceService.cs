using Microsoft.EntityFrameworkCore;
using SaEkg.Application.Devices;
using SaEkg.Domain.Entities;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Services;

public sealed class DeviceService(SaEkgDbContext db) : IDeviceService
{
    public async Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await db.Devices
            .OrderBy(x => x.Name)
            .Select(x => new DeviceDto(x.Id, x.Name, x.SerialNumber, x.Model, x.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Devices
            .Where(x => x.Id == id)
            .Select(x => new DeviceDto(x.Id, x.Name, x.SerialNumber, x.Model, x.Description))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeviceDto> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            SerialNumber = request.SerialNumber.Trim(),
            Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        db.Devices.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new DeviceDto(entity.Id, entity.Name, entity.SerialNumber, entity.Model, entity.Description);
    }

    public async Task<DeviceDto?> UpdateAsync(Guid id, UpdateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name.Trim();
        entity.Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return new DeviceDto(entity.Id, entity.Name, entity.SerialNumber, entity.Model, entity.Description);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, Guid deletedBy, CancellationToken cancellationToken = default)
    {
        var entity = await db.Devices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DeletedBy = deletedBy;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
