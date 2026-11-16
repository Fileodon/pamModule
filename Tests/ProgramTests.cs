using System.Diagnostics;
using System.Security.Cryptography;
using Tests.Extensions;

namespace Tests;

public class ProgramTests
{
    [Fact]
    public async Task Program_ReturnsSuccess_WhenNoTokenIsProvided()
    {
        ProcessResult result = await RunProgramAsync(string.Empty, null, null);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No PAM token provided.", result.StandardOutput);
    }

    [Fact]
    public async Task Program_ReturnsSuccess_WhenTokenIsValid()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();
        const string audience = "Server A";
        string token = TestableTokenService.CreateAndSignToken(
            rsa.ExportRSAPrivateKeyPem(),
            "SFTP2GO Master Server",
            audience);

        ProcessResult result = await RunProgramAsync(
            token,
            rsa.ExportRSAPublicKeyPem(),
            audience);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Program_ReturnsFailure_WhenTokenIsInvalid()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();
        const string audience = "Server A";
        string token = TestableTokenService.CreateAndSignToken(
            rsa.ExportRSAPrivateKeyPem(),
            "SFTP2GO Master Server",
            audience);

        ProcessResult result = await RunProgramAsync(
            token,
            rsa.ExportRSAPublicKeyPem(),
            "Different audience");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Token validation failed:", result.StandardOutput);
    }

    private static async Task<ProcessResult> RunProgramAsync(
        string input,
        string? publicKey,
        string? audience)
    {
        string workingDirectory = Directory.CreateTempSubdirectory("pam-authenticator-").FullName;

        try
        {
            if (publicKey is not null)
            {
                await File.WriteAllTextAsync(Path.Combine(workingDirectory, "public.pem"), publicKey);
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(typeof(PAM_Authenticator.TokenService).Assembly.Location);

            if (audience is not null)
            {
                startInfo.Environment["PAM_AUDIENCE"] = audience;
            }

            using Process process = Process.Start(startInfo)!;
            await process.StandardInput.WriteLineAsync(input);
            process.StandardInput.Close();

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new ProcessResult(
                process.ExitCode,
                await standardOutputTask,
                await standardErrorTask);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}