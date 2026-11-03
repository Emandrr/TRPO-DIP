using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaEkg.Application.Auth;
using SaEkg.Application.Devices;
using SaEkg.Application.Ecg;
using SaEkg.Application.Logging;
using SaEkg.Application.Medical;
using SaEkg.Domain.Entities;
using SaEkg.Infrastructure.Auth;
using SaEkg.Infrastructure.DuckDb;
using SaEkg.Infrastructure.Jobs;
using SaEkg.Infrastructure.Logging;
using SaEkg.Infrastructure.Persistence;
using SaEkg.Infrastructure.Pdf;
using SaEkg.Infrastructure.Services;

namespace SaEkg.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=saekg;Username=saekg;Password=saekg";

        services.AddDbContext<SaEkgDbContext>(options =>
            options.UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.Configure<DuckDbOptions>(configuration.GetSection(DuckDbOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));

        services.AddSingleton<IEcgSignalStore, EcgSignalStore>();
        services.AddSingleton<PasswordHasher<User>>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPendingTempPasswordStore, PendingTempPasswordStore>();
        services.AddSingleton<ITempPasswordPdfService, TempPasswordPdfService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<ISystemLogQueryService, SystemLogQueryService>();
        services.AddScoped<IEcgUploadService, EcgUploadService>();
        services.AddScoped<IEcgAnalysisService, EcgAnalysisService>();
        services.AddScoped<IEcgReportService, EcgReportService>();
        services.AddScoped<IMedicalDataService, MedicalDataService>();
        services.AddScoped<IDoctorPatientQueryService, DoctorPatientQueryService>();
        services.AddScoped<IPurgeService, PurgeService>();

        services.AddSingleton<SystemLogChannel>();
        services.AddSingleton<SystemLogsSink>();
        services.AddHostedService<SystemLogsWriterService>();
        services.AddHostedService<AnalyzeRecordWorker>();
        services.AddHostedService<PurgeJobsWorker>();

        return services;
    }
}
