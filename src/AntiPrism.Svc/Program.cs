using AntiPrism.Core.Hardware;
using AntiPrism.Core.Storage;
using AntiPrism.Svc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// 1. Check for command-line unbrick argument
if (args.Length > 0 && args.Any(a => a.Equals("--unbrick", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("Executing emergency camera unbrick...");
    int count = DeviceDisabler.EnableAllCameras();
    Console.WriteLine($"Restored {count} cameras. Exiting.");
    return 0;
}

// 2. Setup Serilog
var logDir = Path.Combine(ConfigManager.GetConfigDirectory(), "logs");
Directory.CreateDirectory(logDir);

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        path: Path.Combine(logDir, "service-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14);

try
{
    if (OperatingSystem.IsWindows())
    {
        loggerConfig.WriteTo.EventLog(
            source: "AntiPrismSvc",
            logName: "Application",
            manageEventSource: true);
    }
}
catch { }

Log.Logger = loggerConfig.CreateLogger();

try
{
    Log.Information("Starting AntiPrism Windows Service Host...");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "AntiPrismSvc";
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    host.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "AntiPrism Windows Service terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
