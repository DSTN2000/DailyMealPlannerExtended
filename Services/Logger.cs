namespace DailyMealPlannerExtended.Services;

using Serilog;
using Serilog.Events;

public static class Logger
{
    private static readonly Lazy<ILogger> _logger = new(CreateLogger);

    public static ILogger Instance => _logger.Value;

    private static ILogger CreateLogger()
    {
        var logDirectory = Path.Combine("bin","logs");
        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                path: Path.Combine(logDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 7
            )
            .CreateLogger();
    }

    public static void CloseAndFlush() => Log.CloseAndFlush();
}
