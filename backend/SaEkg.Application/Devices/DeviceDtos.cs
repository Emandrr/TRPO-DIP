namespace SaEkg.Application.Devices;

public sealed record DeviceDto(
    Guid Id,
    string Name,
    string SerialNumber,
    string? Model,
    string? Description);

public sealed record CreateDeviceRequest(
    string Name,
    string SerialNumber,
    string? Model,
    string? Description);

public sealed record UpdateDeviceRequest(
    string Name,
    string? Model,
    string? Description);
