// Jwt Token
using PAM_Authenticator;

var token = Console.In.ReadLine()?.TrimEnd('\0').Trim(); // pam_exec typically sends a null-terminated string, so we trim it off here.
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("No PAM token provided.");
    return;
}

string audience = Environment.GetEnvironmentVariable("PAM_AUDIENCE")
    ?? Environment.MachineName;

string keyPath = Path.Combine(AppContext.BaseDirectory, "public.pem");
/* PAM
 * Pam expects a return code
 * 0 = success
 * 1 = failure
*/
if (await TokenService.Verify(token, File.ReadAllText(keyPath), audience))
{
    Environment.Exit(0);
}
else
{
    Environment.Exit(1);
}