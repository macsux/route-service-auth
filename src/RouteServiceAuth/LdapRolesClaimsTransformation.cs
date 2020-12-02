using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;
using RouteServiceAuth.Kerberos.NET;

namespace RouteServiceAuth
{
    public class LdapRolesClaimsTransformer : IStartupFilter, IClaimsTransformation
    {
        private readonly LdapOptions _options;
        private Dictionary<string, string> _sidsToGroupNames;

        public LdapRolesClaimsTransformer(IOptions<LdapOptions> options)
        {
            _options = options.Value;
        }

        public void Initialize()
        {
            try
            {
                using var cn = new LdapConnection();
                cn.Connect(_options.Server, _options.Port);
                cn.Bind(_options.Username, _options.Password);
                _sidsToGroupNames = cn.Search(_options.GroupsQuery, LdapConnection.ScopeSub, _options.Filter, null, false)
                    .ToDictionary(x => new SecurityIdentifier(x.GetAttribute("objectSid").ByteValue, 0).Value, x => x.GetAttribute("sAMAccountName").StringValue);
            }
            catch (Exception e)
            {
                throw new AuthenticationException("Failed to load groups from LDAP", e);
            }
        }
        

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                Initialize();
                next(builder);
            };
        }

        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var identity = (ClaimsIdentity)principal.Identity;
            foreach (var sidGroupClaim in identity.Claims.Where(x => x.Type == ClaimTypes.GroupSid).ToList())
            {
                var sid = sidGroupClaim.Value;
                if (!_sidsToGroupNames.TryGetValue(sid, out var group)) continue;
                if (!identity.HasClaim(ClaimTypes.Role, group))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, group));
                }
            }

            return Task.FromResult(principal);
        }
    }
}