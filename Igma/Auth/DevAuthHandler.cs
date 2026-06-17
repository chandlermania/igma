using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Igma.Auth;

public class DevAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var devRole = config.GetValue<string>("App:DevRole") ?? "Writer";
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "dev@localhost"),
            new Claim(ClaimTypes.NameIdentifier, "dev-local"),
            new Claim(ClaimTypes.Role, devRole),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
