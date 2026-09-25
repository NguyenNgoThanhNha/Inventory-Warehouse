using Microsoft.Extensions.Hosting;
using Serilog;

namespace Inventory.Infrastructure.Logging;

/// <summary>Cấu hình Serilog dùng chung (theo Common.Logging của VAS_CRM_BE). Sink/level bổ sung đọc từ appsettings "Serilog".</summary>
public static class Serilogger
{
    public static Action<HostBuilderContext, LoggerConfiguration> Configure =>
        (context, configuration) =>
        {
            var applicationName = context.HostingEnvironment.ApplicationName?.ToLowerInvariant().Replace(".", "-");

            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Enrich.WithProperty("Application", applicationName)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {TraceId}{NewLine}  {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: Path.Combine("logs", $"{applicationName}-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} TraceId={TraceId} UserId={UserId}{NewLine}  {Message:lj}{NewLine}{Exception}");
        };
}
