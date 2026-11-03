using Microsoft.EntityFrameworkCore;
using SaEkg.Application.Auth;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Services;

public sealed class DoctorPatientQueryService(SaEkgDbContext db) : IDoctorPatientQueryService
{
    public async Task<IReadOnlyList<AssignedPatientDto>> ListPatientsAsync(
        Guid doctorId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.Doctor)
        {
            if (doctorId != actorId)
            {
                throw new EcgUploadException("FORBIDDEN", "Недостаточно прав.");
            }
        }
        else if (role != UserRole.Admin)
        {
            throw new EcgUploadException("FORBIDDEN", "Недостаточно прав.");
        }

        if (!await db.Doctors.AnyAsync(x => x.UserId == doctorId, cancellationToken))
        {
            throw new EcgUploadException("NOT_FOUND", "Врач не найден.");
        }

        var links = await db.DoctorPatients.AsNoTracking()
            .Where(x => x.DoctorId == doctorId)
            .OrderByDescending(x => x.AssignedAt)
            .ToListAsync(cancellationToken);
        if (links.Count == 0)
        {
            return Array.Empty<AssignedPatientDto>();
        }

        var patientIds = links.Select(x => x.PatientId).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(x => patientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var patients = await db.Patients.AsNoTracking()
            .Where(x => patientIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        var cards = await db.MedicalCards.AsNoTracking()
            .Where(x => patientIds.Contains(x.PatientId))
            .ToDictionaryAsync(x => x.PatientId, cancellationToken);

        return links
            .Where(l => users.ContainsKey(l.PatientId))
            .Select(l =>
            {
                users.TryGetValue(l.PatientId, out var user);
                patients.TryGetValue(l.PatientId, out var patient);
                cards.TryGetValue(l.PatientId, out var card);
                return new AssignedPatientDto(
                    l.PatientId,
                    user!.Email,
                    patient?.ConsentSigned ?? false,
                    l.AssignedAt,
                    card?.LastName,
                    card?.FirstName,
                    card?.MiddleName);
            })
            .OrderBy(x => x.Email)
            .ToList();
    }
}
