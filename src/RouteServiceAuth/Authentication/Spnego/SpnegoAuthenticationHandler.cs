using System;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Kerberos.NET;
using Kerberos.NET.Crypto;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RouteServiceAuth.Kerberos;

namespace RouteServiceAuth.Authentication.Spnego
{
    public class SpnegoAuthenticationHandler : AuthenticationHandler<SpnegoAuthenticationOptions>
    {

        public SpnegoAuthenticationHandler(
            IOptionsMonitor<SpnegoAuthenticationOptions> optionsMonitor,
            ILoggerFactory loggerFactory,
            UrlEncoder encoder,
            ISystemClock clock) : base(optionsMonitor, loggerFactory, encoder, clock)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string authorizationHeader = Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authorizationHeader)) return AuthenticateResult.NoResult();

            if (!authorizationHeader.StartsWith($"{SpnegoAuthenticationDefaults.AuthenticationScheme} ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }

            var base64Token = authorizationHeader.Substring(SpnegoAuthenticationDefaults.AuthenticationScheme.Length).Trim();

            if (string.IsNullOrEmpty(base64Token))
            {
                const string noCredentialsMessage = "Credentials not supplied as part of Authorization Header";
                Logger.LogTrace(noCredentialsMessage);
                return AuthenticateResult.Fail(noCredentialsMessage);
            }

            try
            {
                Logger.LogTrace("Validating incoming SPNEGO Ticket \n{Ticket}", base64Token);
                
                var keys = Options.Credentials
                    .Select(x => new KerberosKey(x.Password, KerberosHelper.PrincipalFromUsername(x.UserAccount!), saltType: SaltType.ActiveDirectoryUser))
                    .ToArray();

                ClaimsIdentity? identity = null;
                foreach (var key in keys)
                {
                    var keyTable = new KeyTable(key);
                    var authenticator = new KerberosAuthenticator(keyTable) {UserNameFormat = UserNameFormat.DownLevelLogonName};
                    try
                    {
                        identity = await authenticator.Authenticate(base64Token);
                        break;
                    }
                    catch (SecurityException e) when (e is not KerberosValidationException)
                    {
                        // ignore. likely not matching key we're currently trying, just try the next one
                    }
                }

                if (identity == null)
                {
                    throw new KerberosValidationException("None of the credentials provided match the ticket");
                }
                
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