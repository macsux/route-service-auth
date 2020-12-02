using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Kerberos.NET;
using Kerberos.NET.Crypto;
using Kerberos.NET.Entities;
using Microsoft.Extensions.Options;

namespace RouteServiceAuth
{
    public class SpnegoAuthenticator
    {
        private readonly KerberosAuthenticator _authenticator;

        public SpnegoAuthenticator(SpnegoAuthenticationOptions options)
        {
            
            if(options.PrincipalName == null)
                throw new InvalidOperationException(@"Principal name must be in either DOMAIN\account or account@domain format and is case sensitive");
            if(options.PrincipalPassword == null)
                throw new InvalidOperationException(@"Principal password is not specified");
            var split = options.PrincipalName.Split(@"\");
            string realm, principal;
            if (split.Length == 2) // DOMAIN\account
            {
                realm = split[0].ToUpper();
                principal = split[1];
            }
            else
            {
                split = options.PrincipalName.Split("@"); // user@domain
                if (split.Length != 2) // DOMAIN\account
                    throw new InvalidOperationException(@"Principal name must be in either DOMAIN\account or account@domain format and is case sensitive");
                principal = split[0];
                realm = split[1].ToUpper();
            }

            if (options.PrincipalPassword != null)
            {
                var kerberosKey = new KerberosKey(options.PrincipalPassword, new PrincipalName(PrincipalNameType.NT_UNKNOWN, realm, new[] { principal }), saltType: SaltType.ActiveDirectoryUser);
                _authenticator = new KerberosAuthenticator(new KerberosValidator(kerberosKey));
            }
            else
            {
                _authenticator = new KerberosAuthenticator(new KeyTable(File.ReadAllBytes(options.KeytabFile)));
            }

            _authenticator.UserNameFormat = UserNameFormat.DownLevelLogonName;
        }
        

        public async Task<ClaimsIdentity> Authenticate(string base64Token)
        {
            var identity = await _authenticator.Authenticate(base64Token);
            return identity;
        }
        
    }
}