using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OptimaVerifica.Api.Auth;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));
        }

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
            if (authHeader.Scheme != "Basic")
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Scheme"));
            }

            var credentialBytes = Convert.FromBase64String(authHeader.Parameter!);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            var username = credentials[0];
            var password = credentials[1];

            var (isValid, role) = ValidateCredentials(username, password);
            if (!isValid)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid Username or Password"));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role!)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));
        }
    }

    private (bool isValid, string? role) ValidateCredentials(string username, string password)
    {
        var config = Context.RequestServices.GetRequiredService<IConfiguration>();

        var adminUser = config["Auth:AdminUser"];
        var adminPassword = config["Auth:AdminPassword"];
        if (username == adminUser && password == adminPassword)
        {
            return (true, "ADMIN");
        }

        var operatorUser = config["Auth:OperatorUser"];
        var operatorPassword = config["Auth:OperatorPassword"];
        if (username == operatorUser && password == operatorPassword)
        {
            return (true, "OPERATOR");
        }

        var readerUser = config["Auth:ReaderUser"];
        var readerPassword = config["Auth:ReaderPassword"];
        if (username == readerUser && password == readerPassword)
        {
            return (true, "READER");
        }

        return (false, null);
    }
}
