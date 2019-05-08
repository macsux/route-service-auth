using System;
using System.IO;
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

namespace RouteServiceAuth
{
    public class SpnegoAuthenticationHandler : AuthenticationHandler<SpnegoAuthenticationOptions>
    {
        private const string SchemeName = "Negotiate";
        private KerberosAuthenticator _authenticator;
        private readonly IDisposable _monitorHandle;


        public SpnegoAuthenticationHandler(
            IOptionsMonitor<SpnegoAuthenticationOptions> options,
            ILoggerFactory loggerFactory,
            UrlEncoder encoder,
            ISystemClock clock) : base(options, loggerFactory, encoder, clock)
        {
            _monitorHandle = options.OnChange(CreateAuthenticator);
        }

        private void CreateAuthenticator(SpnegoAuthenticationOptions options)
        {
            if (Options.PrincipalPassword != null)
                _authenticator = new KerberosAuthenticator(new KerberosValidator(new KerberosKey(options.PrincipalPassword)));
            else
                _authenticator = new KerberosAuthenticator(new KeyTable(File.ReadAllBytes(Options.KeytabFile)));
            _authenticator.UserNameFormat = UserNameFormat.DownLevelLogonName;
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
            }
            catch (Exception)
            {
                return AuthenticateResult.Fail("Access denied");
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
            CreateAuthenticator(Options);
            return Task.CompletedTask;
        }
    }
}