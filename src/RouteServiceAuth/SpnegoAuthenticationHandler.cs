using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Kerberos.NET;
using Kerberos.NET.Crypto;
using Kerberos.NET.Entities.Pac;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Novell.Directory.Ldap;
using SecurityIdentifier = System.Security.Principal.SecurityIdentifier;

namespace RouteServiceAuth
{
    public class SpnegoAuthenticationHandler : AuthenticationHandler<SpnegoAuthenticationOptions>
    {
        private const string SchemeName = "Negotiate";
        private KerberosAuthenticator _authenticator;
        private readonly IDisposable _monitorHandle;
        private Dictionary<SecurityIdentifier, string> _sidsToGroupNames = new Dictionary<SecurityIdentifier, string>();
        private bool _groupsLoaded = false;


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
            if (Options.LdapServer != null && Options.LdapUsername != null && Options.LdapPassword != null)
            {
                using var cn = new LdapConnection();
                cn.Connect(Options.LdapServer, Options.LdapPort);
                cn.Bind(Options.LdapUsername, Options.LdapPassword);
                _sidsToGroupNames = cn.Search(Options.LdapGroupsQuery, LdapConnection.ScopeSub, options.LdapFilter, null, false)
                    .ToDictionary(x => new SecurityIdentifier(x.GetAttribute("objectSid").ByteValue, 0), x => x.GetAttribute("sAMAccountName").StringValue);
                _groupsLoaded = true;
            }
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
                    MapSidsToGroupNames(identity);

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

        private void MapSidsToGroupNames(ClaimsIdentity identity)
        {
            if (!_groupsLoaded) return;
            foreach (var sidGroupClaim in identity.Claims.Where(x => x.Type == ClaimTypes.GroupSid).ToList())
            {
                var sid = new SecurityIdentifier(sidGroupClaim.Value);
                if (!_sidsToGroupNames.TryGetValue(sid, out var group)) continue;
                if (!identity.HasClaim(ClaimTypes.Role, group))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, group));
                }
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