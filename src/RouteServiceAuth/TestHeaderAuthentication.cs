using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
//using System.Linq;

namespace RouteServiceAuth
{
    /// <summary>
    /// This class is only used for testing and not part of regular execution pipeline
    /// </summary>
    public class TestHeaderAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string Header { get; set; } = "X-Password";
        public string Password { get; set; } = "Password";
    }
    public class TestHeaderAuthenticationHandler : AuthenticationHandler<TestHeaderAuthenticationOptions>
    {
        public TestHeaderAuthenticationHandler(IOptionsMonitor<TestHeaderAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

#pragma warning disable 1998
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
#pragma warning restore 1998
        {
            if(this.Request.Headers.TryGetValue(this.Options.Header, out var password) && password == this.Options.Password)
            {
                var ticket = new AuthenticationTicket(
                    new ClaimsPrincipal(
                        new ClaimsIdentity(new []
                        {
                            new Claim(ClaimTypes.Name,"Andrew"),
                        }, SpnegoAuthenticationDefaults.AuthenticationScheme)), 
                    SpnegoAuthenticationDefaults.AuthenticationScheme);
                return AuthenticateResult.Success(ticket);
            }
            return AuthenticateResult.Fail("Not authorized");
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.Headers.Append(HeaderNames.WWWAuthenticate, $"Negotiate");
            return Task.CompletedTask;
        }
    }
}
