using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
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

        if (TryValidateRole(username, password, config["Auth:AdminUser"], config["Auth:AdminPassword"]))
        {
            return (true, "ADMIN");
        }

        if (TryValidateRole(username, password, config["Auth:OperatorUser"], config["Auth:OperatorPassword"]))
        {
            return (true, "OPERATOR");
        }

        if (TryValidateRole(username, password, config["Auth:ReaderUser"], config["Auth:ReaderPassword"]))
        {
            return (true, "READER");
        }

        return (false, null);
    }

    private static bool TryValidateRole(
        string providedUsername,
        string providedPassword,
        string? expectedUsername,
        string? expectedPassword)
    {
        if (string.IsNullOrWhiteSpace(expectedUsername) || string.IsNullOrWhiteSpace(expectedPassword))
        {
            return false;
        }

        return SecureEquals(providedUsername, expectedUsername)
            && SecureEquals(providedPassword, expectedPassword);
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        var maxLength = Math.Max(leftBytes.Length, rightBytes.Length);

        if (maxLength == 0)
        {
            return true;
        }

        var paddedLeft = new byte[maxLength];
        var paddedRight = new byte[maxLength];

        Buffer.BlockCopy(leftBytes, 0, paddedLeft, 0, leftBytes.Length);
        Buffer.BlockCopy(rightBytes, 0, paddedRight, 0, rightBytes.Length);

        var contentMatches = CryptographicOperations.FixedTimeEquals(paddedLeft, paddedRight);
        return contentMatches && leftBytes.Length == rightBytes.Length;
    }
}
