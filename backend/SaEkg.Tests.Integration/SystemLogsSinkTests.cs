using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaEkg.Domain.Entities;
using SaEkg.Infrastructure.Logging;
using SaEkg.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaEkg.Tests.Integration;

public sealed class SystemLogsSinkTests
{
    [SkippableFact]
    public async Task Service_error_is_written_to_system_logs_with_module_and_code()
    {
        PostgreSqlContainer postgres;
        try
        {
            postgres = new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("saekg")
                .WithUsername("saekg")
                .WithPassword("saekg")
                .Build();
        }
        catch (DockerUnavailableException)
        {
            throw new SkipException("Docker is not available; start Docker Desktop to run system_logs tests.");
        }

        await using (postgres)
        {
            try
            {
                await postgres.StartAsync();
            }
            catch (DockerUnavailableException)
            {
                throw new SkipException("Docker is not available; start Docker Desktop to run system_logs tests.");
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<SaEkgDbContext>(options =>
                options.UseNpgsql(postgres.GetConnectionString()).UseSnakeCaseNamingConvention());
            services.AddSingleton<SystemLogChannel>();
            services.AddSingleton<SystemLogsSink>();
            services.AddHostedService<SystemLogsWriterService>();

            await using var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
                await db.Database.MigrateAsync();
            }

            var sink = provider.GetRequiredService<SystemLogsSink>();
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink, LogEventLevel.Information)
                .CreateLogger();

            var hosted = provider.GetServices<IHostedService>().OfType<SystemLogsWriterService>().Single();
            await hosted.StartAsync(CancellationToken.None);

            Log.ForContext("Module", "CsvValidation")
                .ForContext("Code", "CSV_CHECK_5")
                .Warning("Validation failed");

            SystemLog? row = null;
            for (var i = 0; i < 40; i++)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SaEkgDbContext>();
                row = await db.SystemLogs.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Code == "CSV_CHECK_5");
                if (row is not null)
                {
                    break;
                }

                await Task.Delay(100);
            }

            await hosted.StopAsync(CancellationToken.None);
            await Log.CloseAndFlushAsync();

            row.Should().NotBeNull();
            row!.Module.Should().Be("CsvValidation");
            row.Code.Should().Be("CSV_CHECK_5");
            row.Message.Should().Contain("Validation failed");
        }
    }
}
