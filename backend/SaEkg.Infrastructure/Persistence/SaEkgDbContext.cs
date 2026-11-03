using Microsoft.EntityFrameworkCore;
using SaEkg.Domain.Entities;
using SaEkg.Domain.Enums;

namespace SaEkg.Infrastructure.Persistence;

public class SaEkgDbContext : DbContext
{
    public SaEkgDbContext(DbContextOptions<SaEkgDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<MedicalCard> MedicalCards => Set<MedicalCard>();
    public DbSet<DoctorPatient> DoctorPatients => Set<DoctorPatient>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<EcgRecord> EcgRecords => Set<EcgRecord>();
    public DbSet<EcgGap> EcgGaps => Set<EcgGap>();
    public DbSet<AnalysisReport> AnalysisReports => Set<AnalysisReport>();
    public DbSet<Anomaly> Anomalies => Set<Anomaly>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();
    public DbSet<DiagnosisAnomaly> DiagnosisAnomalies => Set<DiagnosisAnomaly>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<AuditAccess> AuditAccesses => Set<AuditAccess>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t => t.HasCheckConstraint(
                "ck_users_role",
                $"role IN ('{UserRole.Patient}','{UserRole.Doctor}','{UserRole.Admin}')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasColumnType("citext").IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).IsRequired();
            e.Property(x => x.IsBlocked).HasDefaultValue(false);
            e.Property(x => x.MustChangePassword).HasDefaultValue(false);
            e.Property(x => x.FailedLoginCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).IsRequired();
            e.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Patient>(e =>
        {
            e.ToTable("patients");
            e.HasKey(x => x.UserId);
            e.Property(x => x.ConsentSigned).HasDefaultValue(false);
            e.HasOne(x => x.User).WithOne(x => x.Patient).HasForeignKey<Patient>(x => x.UserId);
        });

        modelBuilder.Entity<Doctor>(e =>
        {
            e.ToTable("doctors");
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.User).WithOne(x => x.Doctor).HasForeignKey<Doctor>(x => x.UserId);
        });

        modelBuilder.Entity<MedicalCard>(e =>
        {
            e.ToTable("medical_cards", t => t.HasCheckConstraint(
                "ck_medical_cards_sex",
                $"sex IN ('{Sex.Male}','{Sex.Female}')"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PatientId).IsUnique();
            e.Property(x => x.Sex).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Patient).WithOne(x => x.MedicalCard).HasForeignKey<MedicalCard>(x => x.PatientId);
        });

        modelBuilder.Entity<DoctorPatient>(e =>
        {
            e.ToTable("doctor_patient");
            e.HasKey(x => new { x.DoctorId, x.PatientId });
            e.HasIndex(x => x.PatientId);
            e.HasOne(x => x.Doctor).WithMany(x => x.PatientLinks).HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Patient).WithMany(x => x.DoctorLinks).HasForeignKey(x => x.PatientId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.SerialNumber).IsRequired();
            e.HasIndex(x => x.SerialNumber).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<EcgRecord>(e =>
        {
            e.ToTable("ecg_records");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PatientId, x.UploadedAt });
            e.Property(x => x.StartedAt).HasColumnType("timestamp(3)");
            e.Property(x => x.EndedAt).HasColumnType("timestamp(3)");
            e.Property(x => x.UnusableRatio).HasPrecision(5, 4);
            e.Property(x => x.RawPurged).HasDefaultValue(false);
            e.Property(x => x.FilteredPurged).HasDefaultValue(false);
            e.HasOne(x => x.Patient).WithMany(x => x.EcgRecords).HasForeignKey(x => x.PatientId);
            e.HasOne(x => x.Device).WithMany(x => x.EcgRecords).HasForeignKey(x => x.DeviceId);
        });

        modelBuilder.Entity<EcgGap>(e =>
        {
            e.ToTable("ecg_gaps", t => t.HasCheckConstraint(
                "ck_ecg_gaps_reason",
                $"reason IN ('{GapReason.Error}','{GapReason.Gap}')"));
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Record).WithMany(x => x.Gaps).HasForeignKey(x => x.RecordId);
        });

        modelBuilder.Entity<AnalysisReport>(e =>
        {
            e.ToTable("analysis_reports", t => t.HasCheckConstraint(
                "ck_analysis_reports_status",
                $"status IN ('{ReportStatus.Pending}','{ReportStatus.Accepted}','{ReportStatus.Rejected}')"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RecordId).IsUnique();
            e.Property(x => x.SummaryJson).HasColumnType("jsonb").HasColumnName("summary");
            e.HasOne(x => x.Record).WithOne(x => x.AnalysisReport).HasForeignKey<AnalysisReport>(x => x.RecordId);
            e.HasOne(x => x.Decider).WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Anomaly>(e =>
        {
            e.ToTable("anomalies");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReportId);
            e.Property(x => x.ValueMin).HasPrecision(18, 6);
            e.Property(x => x.ValueMax).HasPrecision(18, 6);
            e.HasOne(x => x.Report).WithMany(x => x.Anomalies).HasForeignKey(x => x.ReportId);
        });

        modelBuilder.Entity<Diagnosis>(e =>
        {
            e.ToTable("diagnoses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(5000);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Patient).WithMany(x => x.Diagnoses).HasForeignKey(x => x.PatientId);
            e.HasOne(x => x.Doctor).WithMany(x => x.Diagnoses).HasForeignKey(x => x.DoctorId);
            e.HasOne(x => x.Record).WithMany(x => x.Diagnoses).HasForeignKey(x => x.RecordId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Report).WithMany(x => x.Diagnoses).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DiagnosisAnomaly>(e =>
        {
            e.ToTable("diagnosis_anomalies");
            e.HasKey(x => new { x.DiagnosisId, x.AnomalyId });
            e.HasOne(x => x.Diagnosis).WithMany(x => x.AnomalyLinks).HasForeignKey(x => x.DiagnosisId);
            e.HasOne(x => x.Anomaly).WithMany(x => x.DiagnosisLinks).HasForeignKey(x => x.AnomalyId);
        });

        modelBuilder.Entity<Recommendation>(e =>
        {
            e.ToTable("recommendations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(5000).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(5000);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Diagnosis).WithMany(x => x.Recommendations).HasForeignKey(x => x.DiagnosisId);
        });

        modelBuilder.Entity<Prescription>(e =>
        {
            e.ToTable("prescriptions", t => t.HasCheckConstraint("ck_prescriptions_dates", "end_date >= start_date"));
            e.HasKey(x => x.Id);
            e.Property(x => x.DrugName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(5000);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Diagnosis).WithMany(x => x.Prescriptions).HasForeignKey(x => x.DiagnosisId);
        });

        modelBuilder.Entity<SystemLog>(e =>
        {
            e.ToTable("system_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.DetailsJson).HasColumnType("jsonb").HasColumnName("details");
            e.HasIndex(x => new { x.Ts, x.Level, x.Module });
        });

        modelBuilder.Entity<BackgroundJob>(e =>
        {
            e.ToTable("background_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb").HasColumnName("payload");
        });

        modelBuilder.Entity<AuditAccess>(e =>
        {
            e.ToTable("audit_access");
            e.HasKey(x => x.Id);
        });
    }
}
