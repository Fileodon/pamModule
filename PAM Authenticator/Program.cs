// Jwt Token
using PAM_Authenticator;

var token = Console.In.ReadLine();
if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("No PAM token provided.");
    return;
}

string audience = Environment.GetEnvironmentVariable("PAM_AUDIENCE") ?? string.Empty;

/* PAM
 * Pam expects a return code
 * 0 = success
 * 1 = failure
*/
if (await TokenService.Verify(token, File.ReadAllText("public.pem"), audience))
{
    Environment.Exit(0);
}
else
{
    Environment.Exit(1);
}