namespace SaEkg.Application.Auth;

public interface IAuthService
{
    Task<TokenResponse> RegisterPatientAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
