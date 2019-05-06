using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pivotal.IWA.ServiceLightCore;

namespace RouteService
{
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

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
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
    }
}
