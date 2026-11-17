// Jwt Token
using PAM_Authenticator;
using Serilog;

string logPath = Environment.GetEnvironmentVariable("PAM_LOG_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "logs", "pam-authenticator-.log");
string? logDirectory = Path.GetDirectoryName(Path.GetFullPath(logPath));
if (!string.IsNullOrEmpty(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true)
    .CreateLogger();

int exitCode = 0;
try
{
    Log.Information("PAM authentication request received.");

    var token = Console.In.ReadLine()?.TrimEnd('\0').Trim(); // pam_exec typically sends a null-terminated string, so we trim it off here.
    if (string.IsNullOrEmpty(token))
    {
        Log.Warning("PAM authentication request did not include a token.");
        Console.WriteLine("No PAM token provided.");
        exitCode = 1;
        return;
    }

    string audience = Environment.GetEnvironmentVariable("PAM_AUDIENCE")
        ?? Environment.MachineName;

    Log.Information("Our PAM audience is {Audience}.", audience);

    string keyPath = Path.Combine(AppContext.BaseDirectory, "public.pem");
    /* PAM
     * Pam expects a return code
     * 0 = success
     * 1 = failure
    */
    if (await TokenService.Verify(token, File.ReadAllText(keyPath), audience))
    {
        Log.Information("PAM authentication succeeded.");
    }
    else
    {
        Log.Warning("PAM authentication failed.");
        exitCode = 1;
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "PAM authentication terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

Environment.Exit(exitCode);