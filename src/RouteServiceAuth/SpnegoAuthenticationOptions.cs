using System;
using System.IO;
using Microsoft.AspNetCore.Authentication;

namespace RouteServiceAuth
{
    public class SpnegoAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string LdapFilter { get; set; } = "(objectClass=group)";
        public string LdapGroupsQuery { get; set; }

        public string LdapServer { get; set; }
        public int LdapPort { get; set; } = 389;
        public string LdapUsername { get; set; }
        public string LdapPassword { get; set; }

        public string PrincipalPassword { get; set; } = Environment.GetEnvironmentVariable("PRINCIPAL_PASSWORD");

        /// <summary>
        ///     The location of the keytab containing service credentials
        /// </summary>
        public string KeytabFile { get; set; } = Environment.GetEnvironmentVariable("KRB5_CLIENT_KTNAME");

        public override void Validate()
        {

            if (PrincipalPassword == null && KeytabFile == null)
            {
                throw new InvalidOperationException("Must set either principal password or keytab");
            }

            if (PrincipalPassword == null && !File.Exists(KeytabFile))
            {
                throw new InvalidOperationException($"Keytab not found at {KeytabFile}");
            }
        }
    }
}