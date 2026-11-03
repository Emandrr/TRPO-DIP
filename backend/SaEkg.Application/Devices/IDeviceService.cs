namespace SaEkg.Application.Devices;

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<DeviceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeviceDto> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default);
    Task<DeviceDto?> UpdateAsync(Guid id, UpdateDeviceRequest request, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, Guid deletedBy, CancellationToken cancellationToken = default);
}
