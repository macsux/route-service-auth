using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Kerberos.NET;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace RouteServiceAuth
{
    public class SpnegoAuthenticationHandler : AuthenticationHandler<SpnegoAuthenticationOptions>
    {
        protected SpnegoAuthenticator _authenticator;
        private const string SchemeName = "Negotiate";


        public SpnegoAuthenticationHandler(
            IOptionsMonitor<SpnegoAuthenticationOptions> optionsMonitor,
            ILoggerFactory loggerFactory,
            UrlEncoder encoder,
            ISystemClock clock) : base(optionsMonitor, loggerFactory, encoder, clock)
        {
            var options = optionsMonitor.CurrentValue;
            _authenticator = new SpnegoAuthenticator(options);
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authorizationHeader = Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authorizationHeader)) return AuthenticateResult.NoResult();

            if (!authorizationHeader.StartsWith("Negotiate ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

            var base64Token = authorizationHeader.Substring(SchemeName.Length).Trim();

            if (string.IsNullOrEmpty(base64Token))
            {
                const string noCredentialsMessage = "No credentials";
                Logger.LogInformation(noCredentialsMessage);
                return AuthenticateResult.Fail(noCredentialsMessage);
            }

            try
            {
                Logger.LogTrace("===SPNEGO Token===");
                Logger.LogTrace(base64Token);
                var identity = await _authenticator.Authenticate(base64Token);

                var ticket = new AuthenticationTicket(
                    new ClaimsPrincipal(identity),
                    new AuthenticationProperties(),
                    SpnegoAuthenticationDefaults.AuthenticationScheme);
                return AuthenticateResult.Success(ticket);
            }
            catch (KerberosValidationException e)
            {
                return AuthenticateResult.Fail(e);
            }
            catch (Exception e)
            {
                return AuthenticateResult.Fail(e.ToString());
            }
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            
            Response.StatusCode = 401;
            Response.Headers.Append(HeaderNames.WWWAuthenticate, $"Negotiate");
            return Task.CompletedTask;
        }

        protected override Task InitializeHandlerAsync()
        {
            return Task.CompletedTask;
        }
    }
}