using SaEkg.Application.Logging;

namespace SaEkg.Application.Auth;

public interface IAdminUserService
{
    Task<PagedResult<UserDto>> ListAsync(UserQuery query, CancellationToken cancellationToken = default);
    Task<CreatedStaffUserResponse> CreateStaffAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> SetBlockedAsync(Guid id, bool blocked, CancellationToken cancellationToken = default);
    Task<TempPasswordResponse?> ResetTempPasswordAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetConsentAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task AssignAsync(AssignmentRequest request, Guid assignedBy, CancellationToken cancellationToken = default);
    Task UnassignAsync(AssignmentRequest request, CancellationToken cancellationToken = default);
}
