using System;
using System.IO;
using Microsoft.AspNetCore.Authentication;

namespace RouteServiceAuth
{
    public class SpnegoAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        ///     Kerberos SPN used to accept the ticket. The password to this principal should exist in keytab.
        ///     Ex. http/www.myservice.com@DOMAIN.COM OR myserviceaccount@DOMAIN.COM
        /// </summary>
//        public string ServicePrincipalName { get; set; }
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