using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaEkg.Application.Auth;
using SaEkg.Application.Logging;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Infrastructure.Auth;

public sealed class AdminUserService(
    SaEkgDbContext db,
    PasswordHasher<User> passwordHasher,
    IPendingTempPasswordStore pendingTempPasswords) : IAdminUserService
{
    public async Task<PagedResult<UserDto>> ListAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;
        var q = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            q = q.Where(x => x.Role == query.Role);
        }

        if (query.Blocked is not null)
        {
            q = q.Where(x => x.IsBlocked == query.Blocked);
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            var email = query.Email.Trim().ToLowerInvariant();
            q = q.Where(x => x.Email.Contains(email));
        }

        var total = await q.LongCountAsync(cancellationToken);
        var items = await q.OrderBy(x => x.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserDto(x.Id, x.Email, x.Role, x.IsBlocked, x.MustChangePassword, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<UserDto>(items, page, pageSize, total);
    }

    public async Task<CreatedStaffUserResponse> CreateStaffAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AuthException("VALIDATION_ERROR", "Укажите корректный email.");
        }

        if (request.Role is not (UserRole.Doctor or UserRole.Admin))
        {
            throw new AuthException("VALIDATION_ERROR", "Можно создать только роли Doctor или Admin.");
        }

        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new AuthException("CONFLICT", "Пользователь с таким email уже существует.");
        }

        var temp = TempPasswordGenerator.Generate();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Role = request.Role,
            MustChangePassword = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, temp);
        db.Users.Add(user);
        if (request.Role == UserRole.Doctor)
        {
            db.Doctors.Add(new Doctor { UserId = user.Id });
        }

        await db.SaveChangesAsync(cancellationToken);
        pendingTempPasswords.Store(user.Id, user.Email, temp);
        return new CreatedStaffUserResponse(
            new UserDto(user.Id, user.Email, user.Role, user.IsBlocked, user.MustChangePassword, user.CreatedAt),
            temp);
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AuthException("VALIDATION_ERROR", "Укажите корректный email.");
        }

        if (await db.Users.AnyAsync(x => x.Email == email && x.Id != id, cancellationToken))
        {
            throw new AuthException("CONFLICT", "Пользователь с таким email уже существует.");
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (request.Role is not (UserRole.Patient or UserRole.Doctor or UserRole.Admin))
            {
                throw new AuthException("VALIDATION_ERROR", "Недопустимая роль.");
            }

            await EnsureRoleSideTablesAsync(user, request.Role, cancellationToken);
            user.Role = request.Role;
        }

        user.Email = email;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new UserDto(user.Id, user.Email, user.Role, user.IsBlocked, user.MustChangePassword, user.CreatedAt);
    }

    public async Task<bool> SetBlockedAsync(Guid id, bool blocked, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.IsBlocked = blocked;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (blocked)
        {
            var now = DateTimeOffset.UtcNow;
            var tokens = await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.RevokedAt = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TempPasswordResponse?> ResetTempPasswordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var temp = TempPasswordGenerator.Generate();
        user.PasswordHash = passwordHasher.HashPassword(user, temp);
        user.MustChangePassword = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        var now = DateTimeOffset.UtcNow;
        var tokens = await db.RefreshTokens.Where(x => x.UserId == id && x.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        pendingTempPasswords.Store(user.Id, user.Email, temp);
        return new TempPasswordResponse(temp);
    }

    public async Task<bool> SetConsentAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(x => x.UserId == patientId, cancellationToken);
        if (patient is null)
        {
            return false;
        }

        patient.ConsentSigned = true;
        patient.ConsentSignedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AssignAsync(AssignmentRequest request, Guid assignedBy, CancellationToken cancellationToken = default)
    {
        var doctorExists = await db.Doctors.AnyAsync(x => x.UserId == request.DoctorId, cancellationToken);
        var patientExists = await db.Patients.AnyAsync(x => x.UserId == request.PatientId, cancellationToken);
        if (!doctorExists || !patientExists)
        {
            throw new AuthException("NOT_FOUND", "Врач или пациент не найден.");
        }

        var exists = await db.DoctorPatients.AnyAsync(
            x => x.DoctorId == request.DoctorId && x.PatientId == request.PatientId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        db.DoctorPatients.Add(new DoctorPatient
        {
            DoctorId = request.DoctorId,
            PatientId = request.PatientId,
            AssignedAt = DateTimeOffset.UtcNow,
            AssignedBy = assignedBy
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignAsync(AssignmentRequest request, CancellationToken cancellationToken = default)
    {
        var link = await db.DoctorPatients.FirstOrDefaultAsync(
            x => x.DoctorId == request.DoctorId && x.PatientId == request.PatientId,
            cancellationToken);
        if (link is null)
        {
            throw new AuthException("NOT_FOUND", "Закрепление не найдено.");
        }

        db.DoctorPatients.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRoleSideTablesAsync(User user, string newRole, CancellationToken cancellationToken)
    {
        if (newRole == UserRole.Patient && user.Patient is null &&
            !await db.Patients.AnyAsync(x => x.UserId == user.Id, cancellationToken))
        {
            db.Patients.Add(new Patient { UserId = user.Id });
        }

        if (newRole == UserRole.Doctor &&
            !await db.Doctors.AnyAsync(x => x.UserId == user.Id, cancellationToken))
        {
            db.Doctors.Add(new Doctor { UserId = user.Id });
        }
    }
}
