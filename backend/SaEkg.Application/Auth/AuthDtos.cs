namespace SaEkg.Application.Auth;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    bool MustChangePassword,
    string Role,
    Guid UserId);

public sealed record CreateStaffUserRequest(string Email, string Role);

public sealed record UpdateUserRequest(string Email, string? Role);

public sealed record UserDto(
    Guid Id,
    string Email,
    string Role,
    bool IsBlocked,
    bool MustChangePassword,
    DateTimeOffset CreatedAt);

public sealed record CreatedStaffUserResponse(UserDto User, string TemporaryPassword);

public sealed record TempPasswordResponse(string TemporaryPassword);

public sealed record AssignmentRequest(Guid DoctorId, Guid PatientId);

public sealed record UserQuery(string? Role, bool? Blocked, string? Email, int Page, int PageSize);
