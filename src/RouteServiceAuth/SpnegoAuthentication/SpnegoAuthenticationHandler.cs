using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Kerberos.NET;
using Kerberos.NET.Crypto;
using Kerberos.NET.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RouteServiceAuth.Kerberos.NET;

namespace RouteServiceAuth
{
    public class SpnegoAuthenticationHandler : AuthenticationHandler<SpnegoAuthenticationOptions>
    {
        private const string SchemeName = "Negotiate";


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
                var identity = await GetKerberosAuthenticator().Authenticate(base64Token);

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

        private KerberosAuthenticator GetKerberosAuthenticator()
        {
            if(Options.PrincipalName == null)
                throw new InvalidOperationException(@"Principal name must be in either DOMAIN\account or account@domain format and is case sensitive");
            if(Options.PrincipalPassword == null)
                throw new InvalidOperationException(@"Principal password is not specified");

            var principal = KerberosHelper.PrincipalFromUsername(Options.PrincipalName);

            KerberosAuthenticator authenticator;
            if (Options.PrincipalPassword != null)
            {
                var kerberosKey = new KerberosKey(Options.PrincipalPassword, principal, saltType: SaltType.ActiveDirectoryUser);
                authenticator = new KerberosAuthenticator(new KerberosValidator(kerberosKey));
            }
            else
            {
                authenticator = new KerberosAuthenticator(new KeyTable(File.ReadAllBytes(Options.KeytabFile)));
            }

            authenticator.UserNameFormat = UserNameFormat.DownLevelLogonName;
            return authenticator;
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