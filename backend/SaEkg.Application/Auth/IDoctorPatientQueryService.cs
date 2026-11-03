namespace SaEkg.Application.Auth;

public interface IDoctorPatientQueryService
{
    Task<IReadOnlyList<AssignedPatientDto>> ListPatientsAsync(Guid doctorId, Guid actorId, string role, CancellationToken cancellationToken = default);
}

public sealed record AssignedPatientDto(
    Guid PatientId,
    string Email,
    bool ConsentSigned,
    DateTimeOffset AssignedAt,
    string? LastName,
    string? FirstName,
    string? MiddleName);
