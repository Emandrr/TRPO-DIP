using System.Text.Json;
using Serilog.Core;
using Serilog.Events;

namespace SaEkg.Infrastructure.Logging;

public sealed class SystemLogsSink : ILogEventSink
{
    private readonly SystemLogChannel _channel;

    public SystemLogsSink(SystemLogChannel channel)
    {
        _channel = channel;
    }

    public void Emit(LogEvent logEvent)
    {
        var sourceContext = GetString(logEvent, "SourceContext") ?? string.Empty;
        if (sourceContext.Contains("SystemLogsWriterService", StringComparison.Ordinal))
        {
            return;
        }

        var module = GetString(logEvent, "Module");
        if (string.IsNullOrWhiteSpace(module))
        {
            if (logEvent.Level < LogEventLevel.Warning)
            {
                return;
            }

            module = "App";
        }

        var code = GetString(logEvent, "Code");
        var requestId = GetString(logEvent, "RequestId");
        Guid? userId = null;
        if (logEvent.Properties.TryGetValue("UserId", out var userProp)
            && Guid.TryParse(Unwrap(userProp), out var parsedUser))
        {
            userId = parsedUser;
        }

        string? detailsJson = null;
        if (logEvent.Exception is not null || logEvent.Properties.Count > 0)
        {
            var bag = new Dictionary<string, object?>();
            if (logEvent.Exception is not null)
            {
                bag["exceptionType"] = logEvent.Exception.GetType().FullName;
                bag["exceptionMessage"] = logEvent.Exception.Message;
            }

            foreach (var (key, value) in logEvent.Properties)
            {
                if (key is "Module" or "Code" or "RequestId" or "UserId")
                {
                    continue;
                }

                bag[key] = Unwrap(value);
            }

            if (bag.Count > 0)
            {
                detailsJson = JsonSerializer.Serialize(bag);
            }
        }

        _channel.TryWrite(new SystemLogEntry
        {
            Ts = logEvent.Timestamp.ToUniversalTime(),
            Level = logEvent.Level.ToString(),
            Module = module,
            Code = code,
            Message = logEvent.RenderMessage(),
            UserId = userId,
            RequestId = requestId,
            DetailsJson = detailsJson
        });
    }

    private static string? GetString(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out var value) ? Unwrap(value) : null;

    private static string Unwrap(LogEventPropertyValue value) =>
        value switch
        {
            ScalarValue { Value: string s } => s,
            ScalarValue { Value: { } o } => o.ToString() ?? string.Empty,
            _ => value.ToString().Trim('"')
        };
}
