using System;
using Kerberos.NET.Entities;

namespace RouteServiceAuth.Kerberos
{
    public class KerberosHelper
    {
        public static PrincipalName PrincipalFromUsername(string username)
        {
            var split = username.Split(@"\");
            string realm, principal;
            if (split.Length == 2) // DOMAIN\account
            {
                realm = split[0].ToUpper();
                principal = split[1];
            }
            else
            {
                split = username.Split("@"); // user@domain
                if (split.Length != 2) // DOMAIN\account
                    throw new InvalidOperationException(@"Principal name must be in either DOMAIN\account or account@domain format and is case sensitive");
                principal = split[0];
                realm = split[1].ToUpper();
            }

            return new PrincipalName(PrincipalNameType.NT_UNKNOWN, realm, new[] {principal});
        }
    }
}