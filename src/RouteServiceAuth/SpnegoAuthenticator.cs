using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Kerberos.NET;
using Kerberos.NET.Crypto;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;

namespace RouteServiceAuth
{
    public class SpnegoAuthenticator
    {
        private readonly SpnegoAuthenticationOptions _options = new SpnegoAuthenticationOptions();
        // public SpnegoAuthenticationOptions Options => _options.CurrentValue;
        private bool _groupsLoaded = false;
        private KerberosAuthenticator _authenticator;
        private IDisposable _monitorHandle;
        private Dictionary<SecurityIdentifier, string> _sidsToGroupNames = new Dictionary<SecurityIdentifier, string>();


        public SpnegoAuthenticator(IConfiguration configuration)
        {
            configuration.Bind(_options);
            // _monitorHandle = options.OnChange(CreateAuthenticator);
            CreateAuthenticator(_options);
        }


        private void CreateAuthenticator(SpnegoAuthenticationOptions options)
        {
            if (options.PrincipalPassword != null)
                _authenticator = new KerberosAuthenticator(new KerberosValidator(new KerberosKey(options.PrincipalPassword)));
            else
                _authenticator = new KerberosAuthenticator(new KeyTable(File.ReadAllBytes(options.KeytabFile)));
            _authenticator.UserNameFormat = UserNameFormat.DownLevelLogonName;
            if (options.Ldap.Server != null && options.Ldap.Username != null && options.Ldap.Password != null)
            {
                try
                {
                    using var cn = new LdapConnection();
                    cn.Connect(options.Ldap.Server, options.Ldap.Port);
                    cn.Bind(options.Ldap.Username, options.Ldap.Password);
                    _sidsToGroupNames = cn.Search(options.Ldap.GroupsQuery, LdapConnection.ScopeSub, options.Ldap.Filter, null, false)
                        .ToDictionary(x => new SecurityIdentifier(x.GetAttribute("objectSid").ByteValue, 0), x => x.GetAttribute("sAMAccountName").StringValue);
                    _groupsLoaded = true;
                }
                catch (Exception e)
                {
                    throw new AuthenticationException("Failed to load groups from LDAP", e);
                }
            }
        }

        public async Task<ClaimsIdentity> Authenticate(string base64Token)
        {
            var identity = await _authenticator.Authenticate(base64Token);
            MapSidsToGroupNames(identity);
            return identity;
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
    }
}